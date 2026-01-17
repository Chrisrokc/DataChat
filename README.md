# DataChat

A modern enterprise AI chat application with RAG (Retrieval-Augmented Generation) capabilities. Chat with an AI that understands your documents and databases.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2025-CC2927)
![OpenAI](https://img.shields.io/badge/OpenAI-API-412991)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **AI-Powered Chat** - Interactive chat interface with streaming responses powered by OpenAI GPT models
- **RAG (Retrieval-Augmented Generation)** - AI responses grounded in your documents and data
- **File Uploads** - Upload images, PDFs, and documents directly in chat for AI analysis
- **SQL Data Sources** - Connect to SQL Server databases and query tables/views as knowledge sources
- **File System Indexing** - Index documents from folders with pattern matching
- **Vector Search** - SQL Server 2025 native VECTOR type for semantic search
- **User Management** - Role-based access control with local or Windows authentication
- **Data Source Permissions** - Control who can access which data sources
- **Admin Dashboard** - Configure AI settings, manage users, monitor sync jobs

## Screenshots

*Coming soon*

## Technology Stack

| Category | Technologies |
|----------|-------------|
| **Backend** | .NET 8, ASP.NET Core, Entity Framework Core 8 |
| **Frontend** | Blazor Server, Microsoft Fluent UI |
| **Database** | SQL Server 2025 (native VECTOR support) |
| **AI** | OpenAI API (GPT-4o, GPT-4, GPT-3.5-turbo) |
| **Architecture** | Clean Architecture, CQRS with MediatR |
| **Real-time** | SignalR for streaming responses |

## Prerequisites

Before you begin, ensure you have the following installed:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server 2025](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (required for native VECTOR type)
- [OpenAI API Key](https://platform.openai.com/api-keys)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/DataChat.git
cd DataChat
```

### 2. Configure the Database Connection

Edit `src/Presentation/DataChat.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=DataChat;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

**For Windows Authentication:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=DataChat;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

### 3. Run the Application

```bash
cd src/Presentation/DataChat.Web
dotnet run
```

The application will:
- Automatically create the database if it doesn't exist
- Run all EF Core migrations
- Start on `http://localhost:5159`

### 4. Initial Setup

1. **Create Admin Account**

   Navigate to `http://localhost:5159/api/setup-admin` to set the admin password.

   Default credentials:
   - Username: `admin`
   - Password: (set via the setup endpoint)

2. **Configure OpenAI API Key**

   - Log in as admin
   - Go to **Admin > Configuration > AI Settings**
   - Enter your OpenAI API key
   - Select your preferred model (gpt-4o recommended)
   - Click **Save**

3. **Test the Configuration**

   - Go to **Admin > Configuration > Test Chat**
   - Send a test message to verify everything works

## Setting Up Example Data

To test the RAG functionality with sample data, you can create an example employee directory database.

### 1. Create a Sample Database

Connect to your SQL Server and run:

```sql
-- Create a sample database for testing
CREATE DATABASE CompanyData;
GO

USE CompanyData;
GO

-- Create Departments table
CREATE TABLE Departments (
    DepartmentId INT PRIMARY KEY IDENTITY(1,1),
    DepartmentName NVARCHAR(100) NOT NULL,
    Location NVARCHAR(100),
    Budget DECIMAL(18,2)
);

-- Create Employees table
CREATE TABLE Employees (
    EmployeeId INT PRIMARY KEY IDENTITY(1,1),
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100),
    JobTitle NVARCHAR(100),
    DepartmentId INT FOREIGN KEY REFERENCES Departments(DepartmentId),
    HireDate DATE,
    Salary DECIMAL(18,2),
    ManagerId INT NULL FOREIGN KEY REFERENCES Employees(EmployeeId)
);

-- Insert sample departments
INSERT INTO Departments (DepartmentName, Location, Budget) VALUES
('Engineering', 'Building A, Floor 3', 2500000.00),
('Marketing', 'Building B, Floor 1', 800000.00),
('Human Resources', 'Building A, Floor 1', 400000.00),
('Finance', 'Building C, Floor 2', 600000.00),
('Sales', 'Building B, Floor 2', 1200000.00),
('IT Support', 'Building A, Floor 2', 350000.00),
('Legal', 'Building C, Floor 3', 500000.00),
('Product', 'Building A, Floor 4', 900000.00);

-- Insert sample employees
INSERT INTO Employees (FirstName, LastName, Email, JobTitle, DepartmentId, HireDate, Salary, ManagerId) VALUES
-- Engineering
('Sarah', 'Chen', 'sarah.chen@company.com', 'VP of Engineering', 1, '2018-03-15', 185000.00, NULL),
('Michael', 'Johnson', 'michael.johnson@company.com', 'Senior Software Engineer', 1, '2019-06-01', 145000.00, 1),
('Emily', 'Williams', 'emily.williams@company.com', 'Software Engineer', 1, '2021-02-14', 115000.00, 2),
('David', 'Kim', 'david.kim@company.com', 'Software Engineer', 1, '2022-08-22', 105000.00, 2),
('Jessica', 'Martinez', 'jessica.martinez@company.com', 'Junior Developer', 1, '2023-11-01', 75000.00, 2),

-- Marketing
('Robert', 'Brown', 'robert.brown@company.com', 'Marketing Director', 2, '2017-09-10', 155000.00, NULL),
('Amanda', 'Davis', 'amanda.davis@company.com', 'Marketing Manager', 2, '2020-01-20', 95000.00, 6),
('Christopher', 'Wilson', 'chris.wilson@company.com', 'Content Specialist', 2, '2022-04-15', 65000.00, 7),

-- Human Resources
('Jennifer', 'Taylor', 'jennifer.taylor@company.com', 'HR Director', 3, '2016-11-08', 135000.00, NULL),
('Daniel', 'Anderson', 'daniel.anderson@company.com', 'HR Specialist', 3, '2021-07-12', 72000.00, 9),

-- Finance
('Michelle', 'Thomas', 'michelle.thomas@company.com', 'CFO', 4, '2015-05-20', 210000.00, NULL),
('Kevin', 'Garcia', 'kevin.garcia@company.com', 'Financial Analyst', 4, '2020-09-14', 85000.00, 11),

-- Sales
('Brian', 'Rodriguez', 'brian.rodriguez@company.com', 'Sales Director', 5, '2018-02-28', 160000.00, NULL),
('Stephanie', 'Lee', 'stephanie.lee@company.com', 'Account Executive', 5, '2021-05-03', 78000.00, 13),
('Jason', 'White', 'jason.white@company.com', 'Account Executive', 5, '2022-01-17', 75000.00, 13),

-- IT Support
('Nicole', 'Harris', 'nicole.harris@company.com', 'IT Manager', 6, '2019-04-22', 110000.00, NULL),
('Ryan', 'Clark', 'ryan.clark@company.com', 'IT Support Specialist', 6, '2022-06-30', 62000.00, 16),

-- Legal
('Patricia', 'Lewis', 'patricia.lewis@company.com', 'General Counsel', 7, '2017-08-14', 195000.00, NULL),

-- Product
('Andrew', 'Walker', 'andrew.walker@company.com', 'Product Manager', 8, '2020-03-09', 130000.00, NULL),
('Rachel', 'Hall', 'rachel.hall@company.com', 'UX Designer', 8, '2021-10-25', 95000.00, 19);
GO

-- Create a view for the Employee Directory (this is what DataChat will index)
CREATE VIEW vw_EmployeeDirectory AS
SELECT
    e.EmployeeId,
    e.FirstName + ' ' + e.LastName AS FullName,
    e.Email,
    e.JobTitle,
    d.DepartmentName,
    d.Location AS DepartmentLocation,
    e.HireDate,
    e.Salary,
    CASE
        WHEN m.EmployeeId IS NOT NULL
        THEN m.FirstName + ' ' + m.LastName
        ELSE 'N/A'
    END AS ManagerName
FROM Employees e
JOIN Departments d ON e.DepartmentId = d.DepartmentId
LEFT JOIN Employees m ON e.ManagerId = m.EmployeeId;
GO
```

### 2. Add Database Connection in DataChat

1. Log in as admin
2. Go to **Admin > Data Sources**
3. Click **Manage Connections**
4. Click **Add Connection**
5. Fill in:
   - **Name**: `Company Database`
   - **Server**: Your SQL Server address
   - **Database**: `CompanyData`
   - **Authentication**: SQL Server or Windows Auth
   - If SQL Auth, enter username/password
6. Click **Test Connection** to verify
7. Click **Save**

### 3. Create SQL View Data Source

1. Go to **Admin > Data Sources**
2. Click **Add Data Source**
3. Select **SQL View**
4. Fill in:
   - **Name**: `Employee Directory`
   - **Description**: `Company employee information including names, titles, and departments`
   - **Connection**: Select `Company Database`
   - **View/Table Name**: `vw_EmployeeDirectory`
5. Click **Create**
6. Click **Sync Now** to index the data

### 4. Test with Example Questions

Go to the chat and try these questions:

- "Who works in the Engineering department?"
- "What is Sarah Chen's job title?"
- "List all employees hired in 2022"
- "Who is the highest paid employee?"
- "Which department has the largest budget?"
- "Who reports to Robert Brown?"
- "How many employees are in Sales?"

## Configuration Options

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=DataChat;..."
  },
  "Authentication": {
    "Mode": "Local",
    "WindowsAuth": {
      "Enabled": false,
      "AutoProvisionUsers": true,
      "DefaultRole": "User"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### AI Settings (via Admin Panel)

| Setting | Description | Default |
|---------|-------------|---------|
| **OpenAI API Key** | Your OpenAI API key | Required |
| **Chat Model** | GPT model for chat | gpt-4o |
| **Embedding Model** | Model for vector embeddings | text-embedding-ada-002 |
| **Temperature** | Response creativity (0-1) | 0.7 |
| **Max Tokens** | Maximum response length | 2048 |

## Project Structure

```
DataChat/
├── src/
│   ├── Core/
│   │   ├── DataChat.Domain/        # Entities, enums, value objects
│   │   └── DataChat.Application/   # CQRS commands/queries, interfaces
│   ├── Infrastructure/
│   │   └── DataChat.Infrastructure/ # EF Core, OpenAI, vector store
│   └── Presentation/
│       └── DataChat.Web/           # Blazor UI, API endpoints
├── scripts/
│   └── create_test_data.sql                 # Sample data script
└── README.md
```

## Supported File Types

### Chat Attachments
- **Images**: PNG, JPG, GIF, WebP (sent to vision API)
- **PDFs**: Rendered as images for AI analysis
- **Text**: TXT, CSV, MD, JSON (extracted as text)

### Data Source Indexing
- **Documents**: PDF, DOCX, DOC, TXT
- **Data**: SQL Server tables and views

## Authentication Modes

### Local Authentication (Default)
- Username/password stored in database
- Passwords encrypted with data protection
- Session-based with 7-day sliding expiration

### Windows Authentication
Enable in `appsettings.json`:
```json
{
  "Authentication": {
    "Mode": "Windows",
    "WindowsAuth": {
      "Enabled": true,
      "AutoProvisionUsers": true,
      "DefaultRole": "User"
    }
  }
}
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/login` | POST | Authenticate user |
| `/logout` | GET | Sign out |
| `/api/setup-admin` | GET | Reset admin password |
| `/api/auth-test` | GET | Debug auth state |

## Deployment

### Development
```bash
dotnet run --project src/Presentation/DataChat.Web
```

### Production
```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet DataChat.Web.dll
```

### Environment Variables
You can override settings with environment variables:
```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;"
```

## Security Considerations

1. **Change default admin password immediately** after deployment
2. **Use HTTPS** in production
3. **Secure your OpenAI API key** - it's encrypted in the database
4. **Regular backups** - chat history and documents are stored in the database
5. **Review data source permissions** - control who can access sensitive data

## Troubleshooting

### Database Connection Issues
- Verify SQL Server 2025 is installed (required for VECTOR type)
- Check connection string in appsettings.json
- Ensure SQL Server allows TCP/IP connections
- Test with SQL Server Management Studio first

### OpenAI API Issues
- Verify API key is valid and has credits
- Check model availability in your OpenAI account
- Review logs for rate limiting errors

### Vector Search Not Working
- Ensure SQL Server 2025 with native VECTOR support
- Check that data sources are synced (green status)
- Verify embedding model is configured

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [OpenAI](https://openai.com/) for the GPT and embedding models
- [Microsoft Fluent UI](https://developer.microsoft.com/en-us/fluentui) for the component library
- [MediatR](https://github.com/jbogard/MediatR) for the CQRS implementation
