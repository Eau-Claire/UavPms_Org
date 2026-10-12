using System;
using System.Reflection;
using System.Text.Json;
using UavPms.InspectionEvaluationService.Application.Common.Options;
using UavPms.InspectionEvaluationService.Application.Common.Utilities;
using UavPms.InspectionEvaluationService.Domain.Enums;

if (args.Length == 0)
{
    Console.WriteLine("Usage: engine-runner <tc_id|json>");
    return;
}

var command = args[0];

if (command == "test-all")
{
    var results = new Dictionary<string, object>();

    // EVAL_MATH_005
    {
        var opt = new EvaluationThresholdOptions { EmergencyWeight = 80, MaxConfidenceWeight = 80 };
        var res = DetectionEvaluationEngine.Evaluate("T", 0.90, true, opt);
        results["EVAL_MATH_005"] = new
        {
            priority_score = res.PriorityScore,
            severity = res.Severity.ToString(),
            risk_level = res.RiskLevel.ToString()
        };
    }

    // EVAL_MATH_016
    {
        // Craft priority_score = 92: 45 + round(0.8545 * 55) = 45 + 47 = 92
        var opt = new EvaluationThresholdOptions { CriticalScoreThreshold = 95, HighScoreThreshold = 75 };
        var res = DetectionEvaluationEngine.Evaluate("T", 0.8545, true, opt);
        results["EVAL_MATH_016"] = new
        {
            priority_score = res.PriorityScore,
            severity = res.Severity.ToString(),
            risk_level = res.RiskLevel.ToString()
        };
    }

    // EVAL_MATH_017
    {
        var opt = new EvaluationThresholdOptions { EmergencyConfidenceThreshold = 0.50 };
        var res = DetectionEvaluationEngine.Evaluate("T", 0.55, true, opt);
        results["EVAL_MATH_017"] = new
        {
            requires_immediate_alert = res.RequiresImmediateAlert,
            confidence = 0.55,
            threshold = opt.EmergencyConfidenceThreshold
        };
    }

    // EVAL_MATH_018
    {
        var recordType = typeof(DetectionEvaluationEngine.EvaluationResult);
        var props = recordType.GetProperties();
        bool allInitOnly = true;
        foreach (var p in props)
        {
            var setMethod = p.GetSetMethod();
            if (setMethod != null)
            {
                var returnModifiers = setMethod.ReturnParameter.GetRequiredCustomModifiers();
                bool isInit = false;
                foreach (var mod in returnModifiers)
                {
                    if (mod.FullName == "System.Runtime.CompilerServices.IsExternalInit")
                    {
                        isInit = true;
                        break;
                    }
                }
                if (!isInit) allInitOnly = false;
            }
        }
        results["EVAL_MATH_018"] = new
        {
            is_record = recordType.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) != null,
            properties_init_only = allInitOnly,
            property_count = props.Length
        };
    }

    // EVAL_MATH_019
    {
        var opt = new EvaluationThresholdOptions { EmergencyWeight = 100, MaxConfidenceWeight = 0 };
        var res = DetectionEvaluationEngine.Evaluate("T", 0.99, true, opt);
        results["EVAL_MATH_019"] = new
        {
            priority_score = res.PriorityScore,
            severity = res.Severity.ToString(),
            risk_level = res.RiskLevel.ToString()
        };
    }

    // EVAL_MATH_020
    {
        var opt = new EvaluationThresholdOptions { EmergencyWeight = 0, MaxConfidenceWeight = 100 };
        var res = DetectionEvaluationEngine.Evaluate("T", 0.80, true, opt);
        results["EVAL_MATH_020"] = new
        {
            priority_score = res.PriorityScore,
            severity = res.Severity.ToString(),
            risk_level = res.RiskLevel.ToString()
        };
    }

    // EVAL_MATH_021
    {
        var opt = new EvaluationThresholdOptions
        {
            CriticalScoreThreshold = 50,
            HighScoreThreshold = 50,
            MediumScoreThreshold = 50
        };
        // Score = 50: 0 + round(0.9091 * 55) = 50
        var res = DetectionEvaluationEngine.Evaluate("T", 0.9091, false, opt);
        results["EVAL_MATH_021"] = new
        {
            priority_score = res.PriorityScore,
            severity = res.Severity.ToString()
        };
    }

    // EVAL_MATH_024
    {
        results["EVAL_MATH_024"] = new
        {
            Critical = EvaluationSeverity.Critical.ToString(),
            High = EvaluationSeverity.High.ToString(),
            Medium = EvaluationSeverity.Medium.ToString(),
            Low = EvaluationSeverity.Low.ToString()
        };
    }

    // EVAL_MATH_025
    {
        results["EVAL_MATH_025"] = new
        {
            ImmediateAction = EvaluationRiskLevel.ImmediateAction.ToString(),
            UrgentReview = EvaluationRiskLevel.UrgentReview.ToString(),
            PlannedReview = EvaluationRiskLevel.PlannedReview.ToString(),
            Monitor = EvaluationRiskLevel.Monitor.ToString()
        };
    }

    Console.WriteLine(JsonSerializer.Serialize(results));
}
else if (command == "test-config")
{
    var cfgResults = new Dictionary<string, object>();

    // Helper to build service provider with options validation exactly like Program.cs
    static (EvaluationThresholdOptions? Options, Exception? Error) BuildAndResolve(
        Dictionary<string, string?> inMemorySettings,
        string[]? cliArgs = null,
        string? rawJson = null)
    {
        try
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(inMemorySettings);
            if (rawJson != null)
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawJson));
                configBuilder.AddJsonStream(stream);
            }
            if (cliArgs != null)
            {
                configBuilder.AddCommandLine(cliArgs);
            }
            var config = configBuilder.Build();

            var services = new ServiceCollection();
            services.AddOptions<EvaluationThresholdOptions>()
                .Bind(config.GetSection(EvaluationThresholdOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var sp = services.BuildServiceProvider();
            // ValidateOnStart validates when the option is created or on startup
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EvaluationThresholdOptions>>().Value;
            return (opt, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    // EVAL_CFG_001: Default options when section is omitted
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?>());
        cfgResults["EVAL_CFG_001"] = new
        {
            success = err == null,
            EmergencyWeight = opt?.EmergencyWeight,
            MaxConfidenceWeight = opt?.MaxConfidenceWeight,
            EmergencyConfidenceThreshold = opt?.EmergencyConfidenceThreshold,
            CriticalScoreThreshold = opt?.CriticalScoreThreshold,
            HighScoreThreshold = opt?.HighScoreThreshold,
            MediumScoreThreshold = opt?.MediumScoreThreshold
        };
    }

    // EVAL_CFG_002: Custom valid options binding
    {
        var settings = new Dictionary<string, string?>
        {
            ["EvaluationThresholds:EmergencyWeight"] = "50",
            ["EvaluationThresholds:MaxConfidenceWeight"] = "50",
            ["EvaluationThresholds:EmergencyConfidenceThreshold"] = "0.75",
            ["EvaluationThresholds:CriticalScoreThreshold"] = "85",
            ["EvaluationThresholds:HighScoreThreshold"] = "70",
            ["EvaluationThresholds:MediumScoreThreshold"] = "40"
        };
        var (opt, err) = BuildAndResolve(settings);
        cfgResults["EVAL_CFG_002"] = new
        {
            success = err == null,
            EmergencyWeight = opt?.EmergencyWeight,
            MaxConfidenceWeight = opt?.MaxConfidenceWeight,
            EmergencyConfidenceThreshold = opt?.EmergencyConfidenceThreshold,
            CriticalScoreThreshold = opt?.CriticalScoreThreshold,
            HighScoreThreshold = opt?.HighScoreThreshold,
            MediumScoreThreshold = opt?.MediumScoreThreshold
        };
    }

    // EVAL_CFG_003: SectionName constant
    {
        cfgResults["EVAL_CFG_003"] = new
        {
            section_name = EvaluationThresholdOptions.SectionName,
            is_valid = EvaluationThresholdOptions.SectionName == "EvaluationThresholds"
        };
    }

    // Helper for invalid range checks
    void CheckValidationFailure(string tcId, string key, string value, string expectedSubstring)
    {
        var settings = new Dictionary<string, string?> { [$"EvaluationThresholds:{key}"] = value };
        var (opt, err) = BuildAndResolve(settings);
        var validationEx = err as Microsoft.Extensions.Options.OptionsValidationException;
        cfgResults[tcId] = new
        {
            failed = err != null,
            exception_type = err?.GetType().Name,
            error_message = err?.Message,
            contains_expected = err?.Message.Contains(expectedSubstring) == true
        };
    }

    // EVAL_CFG_004: EmergencyWeight negative (-10)
    CheckValidationFailure("EVAL_CFG_004", "EmergencyWeight", "-10", "EmergencyWeight must be between 0 and 100");

    // EVAL_CFG_005: EmergencyWeight exceeds 100 (150)
    CheckValidationFailure("EVAL_CFG_005", "EmergencyWeight", "150", "EmergencyWeight must be between 0 and 100");

    // EVAL_CFG_006: MaxConfidenceWeight negative (-5)
    CheckValidationFailure("EVAL_CFG_006", "MaxConfidenceWeight", "-5", "MaxConfidenceWeight must be between 0 and 100");

    // EVAL_CFG_007: MaxConfidenceWeight exceeds 100 (200)
    CheckValidationFailure("EVAL_CFG_007", "MaxConfidenceWeight", "200", "MaxConfidenceWeight must be between 0 and 100");

    // EVAL_CFG_008: EmergencyConfidenceThreshold negative (-0.1)
    CheckValidationFailure("EVAL_CFG_008", "EmergencyConfidenceThreshold", "-0.1", "EmergencyConfidenceThreshold must be between 0.0 and 1.0");

    // EVAL_CFG_009: EmergencyConfidenceThreshold exceeds 1.0 (1.5)
    CheckValidationFailure("EVAL_CFG_009", "EmergencyConfidenceThreshold", "1.5", "EmergencyConfidenceThreshold must be between 0.0 and 1.0");

    // EVAL_CFG_010: CriticalScoreThreshold invalid (105)
    CheckValidationFailure("EVAL_CFG_010", "CriticalScoreThreshold", "105", "CriticalScoreThreshold must be between 0 and 100");

    // EVAL_CFG_011: HighScoreThreshold invalid (-1)
    CheckValidationFailure("EVAL_CFG_011", "HighScoreThreshold", "-1", "HighScoreThreshold must be between 0 and 100");

    // EVAL_CFG_012: MediumScoreThreshold invalid (101)
    CheckValidationFailure("EVAL_CFG_012", "MediumScoreThreshold", "101", "MediumScoreThreshold must be between 0 and 100");

    // EVAL_CFG_013: Non-numeric string provided for integer weight ("invalid_string")
    {
        var settings = new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyWeight"] = "invalid_string" };
        var (opt, err) = BuildAndResolve(settings);
        cfgResults["EVAL_CFG_013"] = new
        {
            failed = err != null,
            exception_type = err?.GetType().Name,
            is_invalid_operation = err is InvalidOperationException,
            error_message = err?.Message
        };
    }

    // EVAL_CFG_014: Environment variable override (EvaluationThresholds__EmergencyWeight=60)
    {
        try
        {
            Environment.SetEnvironmentVariable("EvaluationThresholds__EmergencyWeight", "60");
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EvaluationThresholds:EmergencyWeight"] = "45"
            });
            configBuilder.AddEnvironmentVariables();
            var config = configBuilder.Build();
            var services = new ServiceCollection();
            services.AddOptions<EvaluationThresholdOptions>()
                .Bind(config.GetSection(EvaluationThresholdOptions.SectionName));
            var sp = services.BuildServiceProvider();
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EvaluationThresholdOptions>>().Value;
            cfgResults["EVAL_CFG_014"] = new
            {
                resolved_weight = opt.EmergencyWeight,
                is_overridden = opt.EmergencyWeight == 60
            };
        }
        finally
        {
            Environment.SetEnvironmentVariable("EvaluationThresholds__EmergencyWeight", null);
        }
    }

    // EVAL_CFG_015: CLI argument override (--EvaluationThresholds:CriticalScoreThreshold=88)
    {
        var (opt, err) = BuildAndResolve(
            new Dictionary<string, string?> { ["EvaluationThresholds:CriticalScoreThreshold"] = "90" },
            cliArgs: new[] { "--EvaluationThresholds:CriticalScoreThreshold=88" }
        );
        cfgResults["EVAL_CFG_015"] = new
        {
            resolved_critical = opt?.CriticalScoreThreshold,
            is_overridden = opt?.CriticalScoreThreshold == 88
        };
    }

    // EVAL_CFG_016: EmergencyWeight = 0 (Min boundary)
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyWeight"] = "0" });
        cfgResults["EVAL_CFG_016"] = new
        {
            success = err == null,
            EmergencyWeight = opt?.EmergencyWeight
        };
    }

    // EVAL_CFG_017: EmergencyWeight = 100 (Max boundary)
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyWeight"] = "100" });
        cfgResults["EVAL_CFG_017"] = new
        {
            success = err == null,
            EmergencyWeight = opt?.EmergencyWeight
        };
    }

    // EVAL_CFG_018: EmergencyConfidenceThreshold = 0.0 (Min boundary)
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyConfidenceThreshold"] = "0.0" });
        cfgResults["EVAL_CFG_018"] = new
        {
            success = err == null,
            EmergencyConfidenceThreshold = opt?.EmergencyConfidenceThreshold
        };
    }

    // EVAL_CFG_019: EmergencyConfidenceThreshold = 1.0 (Max boundary)
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyConfidenceThreshold"] = "1.0" });
        cfgResults["EVAL_CFG_019"] = new
        {
            success = err == null,
            EmergencyConfidenceThreshold = opt?.EmergencyConfidenceThreshold
        };
    }

    // EVAL_CFG_020: Partial section in configuration (only EmergencyWeight = 30)
    {
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?> { ["EvaluationThresholds:EmergencyWeight"] = "30" });
        cfgResults["EVAL_CFG_020"] = new
        {
            success = err == null,
            EmergencyWeight = opt?.EmergencyWeight,
            MaxConfidenceWeight = opt?.MaxConfidenceWeight,
            retains_defaults = opt?.EmergencyWeight == 30 && opt?.MaxConfidenceWeight == 55
        };
    }

    // EVAL_CFG_021: Malformed JSON syntax
    {
        var malformedJson = "{ \"EvaluationThresholds\": { \"EmergencyWeight\": 45 "; // missing closing brace
        var (opt, err) = BuildAndResolve(new Dictionary<string, string?>(), rawJson: malformedJson);
        cfgResults["EVAL_CFG_021"] = new
        {
            failed = err != null,
            is_json_exception = err is System.Text.Json.JsonException || err is FormatException,
            error_message = err?.Message
        };
    }

    // EVAL_CFG_022: OptionsValidationException message contains exact field name
    {
        var settings = new Dictionary<string, string?> { ["EvaluationThresholds:CriticalScoreThreshold"] = "-5" };
        var (opt, err) = BuildAndResolve(settings);
        cfgResults["EVAL_CFG_022"] = new
        {
            failed = err != null,
            contains_field_name = err?.Message.Contains("CriticalScoreThreshold") == true,
            error_message = err?.Message
        };
    }

    Console.WriteLine(JsonSerializer.Serialize(cfgResults));
}
else if (command == "test-http")
{
    var httpResults = new Dictionary<string, object>();

    // EVAL_HTTP_005: PORT=9090 fallback resolution
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PORT"] = "9090" })
            .Build();
        var grpcPort = config.GetValue<int?>("Grpc:Port") ?? config.GetValue<int?>("PORT") ?? 8080;
        httpResults["EVAL_HTTP_005"] = new { grpc_port = grpcPort, matches = grpcPort == 9090 };
    }

    // EVAL_HTTP_006: Health:Port=9091 resolution
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Health:Port"] = "9091" })
            .Build();
        var healthPort = config.GetValue<int?>("Health:Port") ?? 8081;
        httpResults["EVAL_HTTP_006"] = new { health_port = healthPort, matches = healthPort == 9091 };
    }

    // EVAL_HTTP_007: Health:Port equals Grpc:Port (single listener logic)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Grpc:Port"] = "8080", ["Health:Port"] = "8080" })
            .Build();
        var grpcPort = config.GetValue<int?>("Grpc:Port") ?? config.GetValue<int?>("PORT") ?? 8080;
        var healthPort = config.GetValue<int?>("Health:Port") ?? 8081;
        bool hasDualListener = healthPort != grpcPort;
        httpResults["EVAL_HTTP_007"] = new { is_single_listener = !hasDualListener, grpc_port = grpcPort, health_port = healthPort };
    }

    Console.WriteLine(JsonSerializer.Serialize(httpResults));
}
