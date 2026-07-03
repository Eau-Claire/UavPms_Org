using System;
using System.Text;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.Dashboard;
using Hangfire.Dashboard.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UavPms.WebApi.Jobs;

namespace UavPms.WebApi.HangfireExtensions;

public class CreateJobPage : RazorPage
{
    public override void Execute()
    {
        Layout = new LayoutPage("Create Job");

        WriteLiteral(@"
        <style>
            .custom-job-panel {
                background-color: #202530;
                border: 1px solid #2d3748;
                border-radius: 4px;
                padding: 20px;
            }
            .custom-job-title {
                color: #f7fafc;
                margin-top: 0;
                margin-bottom: 20px;
                border-bottom: 1px solid #2d3748;
                padding-bottom: 10px;
                font-size: 20px;
            }
            .custom-sidebar {
                border: 1px solid #2d3748;
                border-radius: 4px;
                background-color: #202530;
                overflow: hidden;
                margin-top: 0px;
            }
            .custom-sidebar-item {
                display: block;
                padding: 10px 15px;
                color: #a0aec0;
                text-decoration: none !important;
                border-bottom: 1px solid #2d3748;
            }
            .custom-sidebar-item:last-child {
                border-bottom: none;
            }
            .custom-sidebar-item.active {
                background-color: #2d3748;
                color: #fff !important;
                font-weight: bold;
            }
            .custom-sidebar-item.disabled {
                color: #4a5568 !important;
                background-color: #1a202c;
                cursor: not-allowed;
            }
            .custom-sidebar-item:hover:not(.active):not(.disabled) {
                background-color: #282e38;
                color: #fff !important;
            }
            .custom-form-label {
                color: #cbd5e0;
                font-weight: 500;
                margin-top: 10px;
            }
            .custom-form-control {
                background-color: #1a202c;
                border: 1px solid #2d3748;
                color: #fff;
                border-radius: 4px;
            }
            .custom-form-control:focus {
                border-color: #4299e1;
                background-color: #1a202c;
                color: #fff;
                box-shadow: none;
            }
            .custom-btn {
                background-color: #3182ce;
                border: none;
                color: #fff;
                padding: 8px 16px;
                border-radius: 4px;
                font-weight: bold;
                margin-top: 15px;
                cursor: pointer;
            }
            .custom-btn:hover {
                background-color: #2b6cb0;
                color: #fff;
            }
        </style>

        <div class=""row"" style=""margin-top: 20px;"">
            <!-- Left Sidebar Navigation -->
            <div class=""col-md-3"">
                <div class=""custom-sidebar"">
                    <a href=""#"" class=""custom-sidebar-item active"">Notification Job</a>
                    <a href=""#"" class=""custom-sidebar-item disabled"">AI Notification (Future)</a>
                </div>
            </div>

            <!-- Right Content Form -->
            <div class=""col-md-9"">
                <div class=""custom-job-panel"">
                    <h3 class=""custom-job-title"">Create Scheduled Job</h3>
                    <form method=""POST"" action=""create-job/submit-custom-job"">
                        <input type=""hidden"" id=""timezoneOffset"" name=""timezoneOffset"" value=""0"" />
                        <div class=""form-group"">
                            <label class=""custom-form-label"" for=""userIds"">User IDs (Comma-separated Guid list, e.g. guid1, guid2, etc. Leave empty for ALL users)</label>
                            <input type=""text"" class=""form-control custom-form-control"" id=""userIds"" name=""userIds"" placeholder=""e.g. c7b508f7-8742-4b2a-a92c-15a0c3bb20e2, 469bfac4-8b96-4f27-a772-945cff2fbaa8"" />
                        </div>
                        <div class=""form-group"">
                            <label class=""custom-form-label"" for=""title"">Title</label>
                            <input type=""text"" class=""form-control custom-form-control"" id=""title"" name=""title"" placeholder=""e.g. System Maintenance"" required />
                        </div>
                        <div class=""form-group"">
                            <label class=""custom-form-label"" for=""body"">Body</label>
                            <textarea class=""form-control custom-form-control"" id=""body"" name=""body"" rows=""4"" placeholder=""Enter notification body details..."" required></textarea>
                        </div>
                        <div class=""form-group"">
                            <label class=""custom-form-label"" for=""type"">Type</label>
                            <input type=""text"" class=""form-control custom-form-control"" id=""type"" name=""type"" value=""ScheduledNotification"" required />
                        </div>
                        <div class=""form-group"">
                            <label class=""custom-form-label"" for=""executeAt"">Execute At</label>
                            <input type=""datetime-local"" class=""form-control custom-form-control"" id=""executeAt"" name=""executeAt"" required />
                        </div>
                        <button type=""submit"" class=""custom-btn"">
                            Schedule Job
                        </button>
                    </form>
                </div>
            </div>
        </div>
        
        <script>
            (function() {
                var now = new Date();
                now.setMinutes(now.getMinutes() + 5);
                
                var tzoffset = now.getTimezoneOffset() * 60000;
                var localISOTime = (new Date(now.getTime() - tzoffset)).toISOString().slice(0, 16);
                
                var input = document.getElementById('executeAt');
                if (input) {
                    input.value = localISOTime;
                }
                
                var offsetInput = document.getElementById('timezoneOffset');
                if (offsetInput) {
                    offsetInput.value = now.getTimezoneOffset();
                }
            })();
        </script>
        ");
    }
}

public class SubmitCustomJobDispatcher : IDashboardDispatcher
{
    public async Task Dispatch(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (!"POST".Equals(httpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = 405; // Method Not Allowed
            return;
        }

        try
        {
            var form = await httpContext.Request.ReadFormAsync();
            var userIds = form["userIds"].ToString();
            var title = form["title"].ToString();
            var body = form["body"].ToString();
            var type = form["type"].ToString();
            var executeAtStr = form["executeAt"].ToString();
            var timezoneOffsetStr = form["timezoneOffset"].ToString();

            if (string.IsNullOrEmpty(type))
            {
                type = "ScheduledNotification";
            }

            var executeAt = DateTime.Now.AddMinutes(5);
            if (!string.IsNullOrWhiteSpace(executeAtStr) && DateTime.TryParse(executeAtStr, out var parsedDateTime))
            {
                executeAt = parsedDateTime;
            }

            var delay = TimeSpan.FromMinutes(5);
            if (int.TryParse(timezoneOffsetStr, out int offsetMinutes))
            {
                var userOffset = TimeSpan.FromMinutes(-offsetMinutes);
                var executeAtOffset = new DateTimeOffset(executeAt, userOffset);
                delay = executeAtOffset - DateTimeOffset.UtcNow;
            }
            else
            {
                delay = executeAt - DateTime.Now;
            }

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            // Schedule the job
            BackgroundJob.Schedule<ScheduledNotificationJob>(
                job => job.SendNotificationAsync(userIds, title, body, type),
                delay);

            // Redirect back to the scheduled jobs page
            httpContext.Response.Redirect(context.Request.PathBase + "/jobs/scheduled");
        }
        catch (Exception)
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsync("Error processing schedule request.");
        }
    }
}

public static class HangfireDashboardCustomizer
{
    public static void ConfigureCustomPages()
    {
        // Add GET page route
        DashboardRoutes.Routes.AddRazorPage("/create-job", x => new CreateJobPage());

        // Add POST dispatcher route
        DashboardRoutes.Routes.Add("/create-job/submit-custom-job", new SubmitCustomJobDispatcher());

        // Add to Navigation Menu
        NavigationMenu.Items.Add(page => new MenuItem("Create Job", page.Url.To("/create-job"))
        {
            Active = page.RequestPath.StartsWith("/create-job", StringComparison.OrdinalIgnoreCase)
        });
    }
}

public class HangfireBasicAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var authHeader = httpContext.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            SetChallengeResponse(httpContext);
            return false;
        }

        try
        {
            var parameter = authHeader.Substring("Basic ".Length).Trim();
            var usernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));
            var parts = usernamePassword.Split(':', 2);

            if (parts.Length == 2)
            {
                var username = parts[0];
                var password = parts[1];

                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var hangfireUser = config["Hangfire:Username"] ?? "admin";
                var hangfirePassword = config["Hangfire:Password"] ?? "123456";

                if (username == hangfireUser && password == hangfirePassword)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore Base64 parsing issues
        }

        SetChallengeResponse(httpContext);
        return false;
    }

    private void SetChallengeResponse(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Hangfire Dashboard\"");
    }
}
