![][image1]   
   
**CAPSTONE PROJECT REGISTER**   
   
**Class**:            **Duration time**:  from 09/2026 To 03/2027  
**(\*) Profession:** Software Engineer                   **Specialty**: SE  ![Hộp Văn bản][image2]                           
**(\*) Kinds of person make registers:**               	Lecturer     ![Hộp Văn bản][image2]           	Students    
   
**1\. Register information for supervisor (if have)** 

| No. | Full name | Phone | E-Mail  | Title  |
| ----- | :---: | :---: | :---: | :---: |
|   Supervisor  | 	Đặng Ngọc Minh Đức |  | ducdnm2@fpt.edu.vn |  |
|   Supervisor | Phạm Minh Trí |  | tripm14@fe.edu.vn |  |

**2\. Register information for students (if have)** 

| No. | Full name  | Student code  | Phone  | E-mail  | Role in Group  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | Nguyễn Quốc Khánh | SE193464 | 0708462865 | khanhnqse19@gmail.com | Leader  |
| 2 | Nguyễn Nhật An | SE193338 | 0898520071 | an3439201@gmail.com | Member  |
| 3 | Trần Ánh Quốc  | SE181995  | 0365837996 | quoctase181995@fpt.edu.vn | Member  |
| 4 | Phạm Hoàng Minh Châu | SE193418 | 0963274717 | phamhoangminhchau1973@gmail.com | Member  |

   
**3\. Register content of Capstone Project**   
**(\*) 3.1. Capstone Project name:**   
3.1.1. English: ***Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure***  
3.1.2. Vietnamese: ***Hệ thống UAV tích hợp AI để kiểm tra và quản lý bảo trì hạ tầng điện lực***

**(\*) 3.2. Main proposal content (including result and product)**     
**a) Context:**  
The modern power-grid sector faces a convergence of two critical demands: efficient, data-driven asset maintenance and the need for reliable electricity supply. Traditional field inspections are labor-intensive, costly, and expose personnel to hazardous environments, while reactive maintenance strategies often result in delayed fault detection and increased operational risks.  
Existing solutions remain fragmented, with separate systems for UAV surveying, defect recognition, and maintenance documentation. This lack of integration creates operational inefficiencies and data silos. Furthermore, many current platforms struggle with inconsistent inspection accuracy and insufficient capacity to analyze high volumes of imagery.  
In addition to automated defect detection, the proposed platform aims to streamline inspection workflows, maintenance management, emergency response, and infrastructure monitoring through a unified software ecosystem. The system introduces an Asset Health Assessment mechanism that evaluates defect severity, inspection history, and maintenance records to support maintenance planning and decision-making.

**b) Proposed Solutions:**  
The system adopts a hybrid AI architecture consisting of:    
\- Edge AI on UAV using lightweight object detection models to support near-real-time anomaly detection under limited computational resources. The edge layer prioritizes inference speed and low computational cost for time-sensitive inspection scenarios.  
\- Cloud AI on Server for more comprehensive post-mission defect analysis. Inspection images and videos are uploaded to the server, where higher-capacity object detection models analyze electrical infrastructure defects. Multiple object detection architectures are evaluated to compare detection accuracy, localization performance, computational requirements, and deployment suitability. The current defect taxonomy includes insulator, insulator broken, insulator flashover, bird nest, foreign object, and broken strand.  
\- Maintenance Management Workflow supporting mission assignment, defect validation, maintenance ticket generation, repair tracking, and verification.  
\- GIS-based Monitoring Dashboard providing visualization of assets, detected defects, emergency alerts, and maintenance activities on an interactive map.

**c) Functional Requirements:**  
User & Access Management  
\-	User authentication and authorization  
\-	Role-Based Access Control (RBAC)  
\-	User profile management  
Mission Management  
\-	Create and manage inspection missions  
\-	Assign missions to inspectors  
\-	Track mission status and progress  
Asset Management  
\-	Manage power-grid assets (Towers)  
\-	Maintain asset history and inspection records  
Inspection Data Management  
\-	Upload inspection images, videos, and flight logs  
\-	Store and retrieve inspection records  
\-	Search inspection data by asset, mission, date, and defect type  
   AI-Based Defect Detection    
\- Perform lightweight anomaly detection at the Edge AI layer for time-sensitive inspection scenarios.  
\- 	Perform detailed defect detection and classification on the Cloud AI server after mission completion.  
\- 	Support multiple object detection models for experimental evaluation and deployment comparison.  
\- 	Visualize detected defects using bounding boxes, confidence scores, and associated infrastructure assets.  
\- 	Store AI detection results for defect validation, maintenance planning, and reporting.  
Emergency Alert Management  
\-	Generate emergency alerts  
\-	Review, confirm, reject, or escalate alerts  
\-	Track alert history and status  
Maintenance Management  
\-	Generate maintenance tickets  
\-	Assign tickets to technicians  
Asset Health Assessment  
\-	Classify assets into risk levels  
\-	Prioritize maintenance actions  
GIS Monitoring  
\-	Visualize assets, defects, alerts, and maintenance tickets on maps  
\-	Filter and search map-based information  
Reporting & Analytics  
\-	Generate operational reports  
\-	Export reports in CSV, Excel, and PDF formats

**d) Non-Functional Requirements:**  
Security  
\-	Secure authentication and authorization  
\-	Role-Based Access Control (RBAC)  
\-	Audit logging of user activities  
\-	Secure data storage and transmission  
Performance  
\-	Dashboard response time \< 2 seconds  
\-	Image processing time \< 5 seconds per image  
\-	Emergency alert delivery latency \< 10 seconds when network connectivity is available  
Reliability  
\-	High availability for mission and maintenance management functions  
\-	Reliable storage of inspection images, videos, and logs  
Scalability  
\-	Support increasing numbers of assets, inspections, and maintenance records  
\-	Modular architecture for future feature expansion  
Usability  
\-	Responsive web interface  
\-	Mobile-friendly interface for maintenance technicians  
\-	Intuitive dashboard and GIS visualization  
Maintainability  
\-	Layered software architecture  
\-	RESTful API design  
\-	Modular AI service integration

**e) Theory & Practical:**  
Theoretical Foundation    
\- 	UAV-based electrical infrastructure inspection  
\- 	Computer Vision and Object Detection  
\- 	CNN-based and Transformer-based Object Detection Models  
\- 	Edge AI and Cloud AI Architecture  
\- 	Object Detection Evaluation Metrics and Model Benchmarking  
\- 	Geographic Information Systems (GIS)  
\- 	Asset Management and Maintenance Management  
\- 	Role-Based Access Control (RBAC)  
Core Technologies  
\-	ASP.NET Core Web API    
\- 	Angular    
\-	PostgreSQL    
\-	Python    
\-	PyTorch    
\-	Ultralytics YOLO    
\-	RT-DETR-based Object Detection Models    
\-	OpenCV    
\-	SignalR    
\-	LeafletJS    
\-	Docker  
Documents to be Delivered  
\-	User Requirement Document (URD)  
\-	Software Requirement Specification (SRS)  
\-	Software Design Document (SDD)  
\-	Test Plan & Test Report  
\-	Deployment Guide  
\-	User Manual  
\-	Final Project Report

**f) Products (Expected Deliverables):**  
Software Products  
\-	Inspection Mission Management System  
\-	Asset Management System  
\-	AI Defect Detection System  
\-	Emergency Alert Management System  
\-	Maintenance Ticket Management System  
\-	GIS Monitoring Dashboard  
\-	Reporting and Analytics Dashboard  
Deliverables  
\-	Source Code  
\-	Database Schema  
\-	Trained AI Models  
\-	Deployment Package  
\-	Technical Documentation  
\-	Testing Documentation

**g) Proposed Tasks:**  
Student 1 – Business Analyst & Project Manager  
\-	Requirement elicitation and analysis  
\-	URD and SRS preparation  
\-	Workflow and business process design  
\-	Project planning and coordination  
\-	UAT preparation  
Student 2 – Backend Developer  
\-	ASP.NET Core Web API development  
\-	PostgreSQL database design  
\-	Authentication and RBAC  
\-	Asset Health Scoring Service  
\-	Emergency Alert Service  
\-	Audit Logging  
Student 3 – Frontend Developer  
\-	ReactJS dashboard development  
\-	GIS visualization using LeafletJS  
\-	Reporting and analytics dashboard  
\-	SignalR integration  
\-	UI/UX implementation  
Student 4 – AI & Data Processing Engineer  
\-	Dataset preparation and preprocessing  
\-	Edge AI emergency detection integration  
\-	Cloud AI defect detection integration  
\-	AI model evaluation and testing  
\-	AI service integration with backend APIs

**3.3. Research Information**  
**a. Research Problem / Research Question:**  
Traditional power-grid inspections are labor-intensive, costly, and often unable to provide timely maintenance decisions. This research investigates how a hybrid UAV–AI platform can integrate emergency anomaly detection, defect analysis, and maintenance management into a unified system.  
**b. Research Objectives:**  
\-	Develop a UAV-assisted inspection and maintenance management platform.  
\-	Design a hybrid Edge AI–Cloud AI architecture for anomaly detection.  
\-	Implement asset health assessment and maintenance prioritization.  
\-	Provide GIS-based monitoring and reporting capabilities.  
**c. Research Scope & Methodology:**  
**Scope:**  
\-	UAV-based inspection.  
\-	Edge AI emergency detection.  
\-	Cloud AI defect analysis.  
\-	Asset health assessment.  
\-	Maintenance workflow and GIS monitoring.  
**Methodology:**  
\-	Literature review.  
\-	System design and development.  
\-	AI integration.  
\-	System testing and evaluation.  
**d. Expected Scientific Contribution:**  
\-	A hybrid Edge AI–Cloud AI architecture for infrastructure inspection.  
\-	An integrated framework combining UAV inspection, AI analysis, and maintenance management.  
\-	A rule-based asset health assessment model for maintenance prioritization.

**4\. Other comments (propose all relative things if have):**   
............................................................................................................................................................  
............................................................................................................................................................  
............................................................................................................................................................

|              Supervisor (If have)   *(Sign and full name)*   | HCM, date 17/08/2026  On behalf of Registers   *(Sign and full name)*   |
| ----- | :---: |

 

[image1]: <data:image/png;base64,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>

[image2]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAAAmklEQVR4Xu2UQQoDIQxF/5G6zsrT5Br2CJ4ml/Ewv6Zj20EGIq2zKMyDgEh4mK+IlBJzzj+XewDc4fjGCtxzSS/pobSyCLyhlbBUo77XQ+uOQLpRixBS6B5TpY0NA1PS/Yk1MnJaSj8uJRj7xaS0ZdnGN48BS8b30T8i0xZDkEEgHbJ8RtBfQ7+4IwLpd/yp9JSfvy9W1Q1n8QDvdQld7Ggx5wAAAABJRU5ErkJggg==>