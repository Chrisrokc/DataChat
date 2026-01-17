-- =====================================================
-- Test Data and Views for Enterprise Chat Bot
-- SQL Server 2025
-- =====================================================

USE EnterpriseChatBot;
GO

-- =====================================================
-- PART 1: Create Sample Business Data Tables
-- =====================================================

-- Drop existing test tables if they exist
IF OBJECT_ID('dbo.Employees', 'U') IS NOT NULL DROP TABLE dbo.Employees;
IF OBJECT_ID('dbo.Departments', 'U') IS NOT NULL DROP TABLE dbo.Departments;
IF OBJECT_ID('dbo.Projects', 'U') IS NOT NULL DROP TABLE dbo.Projects;
IF OBJECT_ID('dbo.ProjectAssignments', 'U') IS NOT NULL DROP TABLE dbo.ProjectAssignments;
IF OBJECT_ID('dbo.SalesOrders', 'U') IS NOT NULL DROP TABLE dbo.SalesOrders;
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DROP TABLE dbo.Customers;
GO

-- Create Departments table
CREATE TABLE dbo.Departments (
    DepartmentId INT PRIMARY KEY IDENTITY(1,1),
    DepartmentName NVARCHAR(100) NOT NULL,
    Budget DECIMAL(18,2) NOT NULL,
    Location NVARCHAR(100),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Create Employees table
CREATE TABLE dbo.Employees (
    EmployeeId INT PRIMARY KEY IDENTITY(1,1),
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    DepartmentId INT FOREIGN KEY REFERENCES dbo.Departments(DepartmentId),
    JobTitle NVARCHAR(100),
    Salary DECIMAL(18,2),
    HireDate DATE,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Create Projects table
CREATE TABLE dbo.Projects (
    ProjectId INT PRIMARY KEY IDENTITY(1,1),
    ProjectName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    StartDate DATE,
    EndDate DATE,
    Budget DECIMAL(18,2),
    Status NVARCHAR(50) DEFAULT 'Active',
    DepartmentId INT FOREIGN KEY REFERENCES dbo.Departments(DepartmentId),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Create ProjectAssignments table
CREATE TABLE dbo.ProjectAssignments (
    AssignmentId INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT FOREIGN KEY REFERENCES dbo.Projects(ProjectId),
    EmployeeId INT FOREIGN KEY REFERENCES dbo.Employees(EmployeeId),
    Role NVARCHAR(100),
    HoursAllocated INT,
    AssignedDate DATE DEFAULT GETDATE()
);

-- Create Customers table
CREATE TABLE dbo.Customers (
    CustomerId INT PRIMARY KEY IDENTITY(1,1),
    CompanyName NVARCHAR(200) NOT NULL,
    ContactName NVARCHAR(100),
    ContactEmail NVARCHAR(100),
    Phone NVARCHAR(20),
    Address NVARCHAR(200),
    City NVARCHAR(100),
    Country NVARCHAR(100),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Create Products table
CREATE TABLE dbo.Products (
    ProductId INT PRIMARY KEY IDENTITY(1,1),
    ProductName NVARCHAR(200) NOT NULL,
    Category NVARCHAR(100),
    UnitPrice DECIMAL(18,2) NOT NULL,
    UnitsInStock INT DEFAULT 0,
    IsDiscontinued BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Create SalesOrders table
CREATE TABLE dbo.SalesOrders (
    OrderId INT PRIMARY KEY IDENTITY(1,1),
    CustomerId INT FOREIGN KEY REFERENCES dbo.Customers(CustomerId),
    ProductId INT FOREIGN KEY REFERENCES dbo.Products(ProductId),
    EmployeeId INT FOREIGN KEY REFERENCES dbo.Employees(EmployeeId),
    OrderDate DATE NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Discount DECIMAL(5,2) DEFAULT 0,
    TotalAmount AS (Quantity * UnitPrice * (1 - Discount/100)),
    Status NVARCHAR(50) DEFAULT 'Pending',
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
GO

-- =====================================================
-- PART 2: Insert Sample Data
-- =====================================================

-- Insert Departments
INSERT INTO dbo.Departments (DepartmentName, Budget, Location) VALUES
('Engineering', 2500000.00, 'Building A - Floor 3'),
('Sales', 1500000.00, 'Building B - Floor 1'),
('Marketing', 800000.00, 'Building B - Floor 2'),
('Human Resources', 500000.00, 'Building A - Floor 1'),
('Finance', 750000.00, 'Building A - Floor 2'),
('Customer Support', 600000.00, 'Building C - Floor 1'),
('Research & Development', 3000000.00, 'Building D - Floor 1'),
('Operations', 1200000.00, 'Building C - Floor 2');

-- Insert Employees
INSERT INTO dbo.Employees (FirstName, LastName, Email, DepartmentId, JobTitle, Salary, HireDate) VALUES
-- Engineering
('John', 'Smith', 'john.smith@company.com', 1, 'Senior Software Engineer', 145000.00, '2020-03-15'),
('Sarah', 'Johnson', 'sarah.johnson@company.com', 1, 'Tech Lead', 165000.00, '2019-06-01'),
('Michael', 'Chen', 'michael.chen@company.com', 1, 'Software Engineer', 120000.00, '2021-09-10'),
('Emily', 'Davis', 'emily.davis@company.com', 1, 'DevOps Engineer', 135000.00, '2020-11-20'),
('David', 'Wilson', 'david.wilson@company.com', 1, 'Junior Developer', 85000.00, '2023-01-15'),
-- Sales
('Jennifer', 'Brown', 'jennifer.brown@company.com', 2, 'Sales Director', 150000.00, '2018-04-10'),
('Robert', 'Taylor', 'robert.taylor@company.com', 2, 'Account Executive', 95000.00, '2021-02-28'),
('Lisa', 'Anderson', 'lisa.anderson@company.com', 2, 'Sales Representative', 75000.00, '2022-05-15'),
('James', 'Martinez', 'james.martinez@company.com', 2, 'Sales Representative', 72000.00, '2022-08-01'),
-- Marketing
('Amanda', 'Garcia', 'amanda.garcia@company.com', 3, 'Marketing Manager', 110000.00, '2019-09-15'),
('Christopher', 'Lee', 'christopher.lee@company.com', 3, 'Content Specialist', 70000.00, '2021-11-01'),
('Jessica', 'White', 'jessica.white@company.com', 3, 'Digital Marketing Analyst', 80000.00, '2022-03-20'),
-- HR
('Michelle', 'Harris', 'michelle.harris@company.com', 4, 'HR Director', 125000.00, '2017-08-01'),
('Daniel', 'Clark', 'daniel.clark@company.com', 4, 'Recruiter', 65000.00, '2021-06-15'),
-- Finance
('Patricia', 'Lewis', 'patricia.lewis@company.com', 5, 'CFO', 200000.00, '2016-01-10'),
('Kevin', 'Walker', 'kevin.walker@company.com', 5, 'Financial Analyst', 90000.00, '2020-07-20'),
('Nancy', 'Hall', 'nancy.hall@company.com', 5, 'Accountant', 75000.00, '2021-04-01'),
-- Customer Support
('Steven', 'Young', 'steven.young@company.com', 6, 'Support Manager', 85000.00, '2019-12-01'),
('Laura', 'King', 'laura.king@company.com', 6, 'Support Specialist', 55000.00, '2022-09-10'),
('Brian', 'Wright', 'brian.wright@company.com', 6, 'Support Specialist', 52000.00, '2023-02-01'),
-- R&D
('Elizabeth', 'Scott', 'elizabeth.scott@company.com', 7, 'Research Director', 180000.00, '2018-02-15'),
('Thomas', 'Green', 'thomas.green@company.com', 7, 'Senior Researcher', 140000.00, '2019-05-01'),
('Rachel', 'Adams', 'rachel.adams@company.com', 7, 'Data Scientist', 130000.00, '2020-10-15'),
-- Operations
('Mark', 'Nelson', 'mark.nelson@company.com', 8, 'Operations Manager', 105000.00, '2018-11-01'),
('Susan', 'Hill', 'susan.hill@company.com', 8, 'Logistics Coordinator', 65000.00, '2021-07-15');

-- Insert Projects
INSERT INTO dbo.Projects (ProjectName, Description, StartDate, EndDate, Budget, Status, DepartmentId) VALUES
('Cloud Migration Initiative', 'Migrate all on-premise infrastructure to AWS cloud', '2024-01-15', '2024-12-31', 500000.00, 'Active', 1),
('Customer Portal Redesign', 'Complete redesign of customer-facing portal with modern UI', '2024-03-01', '2024-09-30', 250000.00, 'Active', 1),
('AI Chatbot Integration', 'Integrate AI-powered chatbot for customer support', '2024-06-01', '2024-11-30', 150000.00, 'Active', 1),
('Q4 Sales Campaign', 'Major sales push for Q4 with new product launch', '2024-10-01', '2024-12-31', 300000.00, 'Planning', 2),
('Brand Refresh 2024', 'Update company branding and marketing materials', '2024-02-01', '2024-06-30', 175000.00, 'Completed', 3),
('Employee Wellness Program', 'Launch comprehensive employee wellness initiative', '2024-04-01', '2024-12-31', 100000.00, 'Active', 4),
('Financial System Upgrade', 'Upgrade ERP and financial reporting systems', '2024-05-01', '2024-10-31', 400000.00, 'Active', 5),
('Next-Gen Product Research', 'Research and development for next generation product line', '2024-01-01', '2025-06-30', 800000.00, 'Active', 7);

-- Insert Project Assignments
INSERT INTO dbo.ProjectAssignments (ProjectId, EmployeeId, Role, HoursAllocated) VALUES
(1, 2, 'Project Lead', 800),
(1, 1, 'Senior Developer', 600),
(1, 4, 'DevOps Lead', 500),
(2, 2, 'Technical Advisor', 200),
(2, 3, 'Lead Developer', 700),
(2, 5, 'Developer', 500),
(3, 1, 'AI Integration Lead', 400),
(3, 3, 'Backend Developer', 300),
(4, 6, 'Campaign Lead', 400),
(4, 7, 'Account Manager', 300),
(4, 8, 'Sales Support', 200),
(5, 10, 'Project Manager', 350),
(5, 11, 'Content Lead', 300),
(5, 12, 'Digital Lead', 250),
(6, 13, 'Program Director', 300),
(6, 14, 'Coordinator', 400),
(7, 15, 'Executive Sponsor', 100),
(7, 16, 'Project Lead', 500),
(7, 17, 'Implementation', 400),
(8, 21, 'Research Lead', 800),
(8, 22, 'Senior Researcher', 700),
(8, 23, 'Data Analysis Lead', 600);

-- Insert Customers
INSERT INTO dbo.Customers (CompanyName, ContactName, ContactEmail, Phone, Address, City, Country) VALUES
('Acme Corporation', 'John Doe', 'john.doe@acme.com', '555-0100', '123 Main St', 'New York', 'USA'),
('Global Industries', 'Jane Smith', 'jane.smith@global.com', '555-0101', '456 Oak Ave', 'Los Angeles', 'USA'),
('Tech Solutions Inc', 'Bob Johnson', 'bob.j@techsol.com', '555-0102', '789 Pine Rd', 'Chicago', 'USA'),
('Premier Services', 'Alice Williams', 'alice@premier.com', '555-0103', '321 Elm St', 'Houston', 'USA'),
('Innovation Labs', 'Charlie Brown', 'charlie@innolabs.com', '555-0104', '654 Cedar Ln', 'Phoenix', 'USA'),
('Digital Dynamics', 'Diana Prince', 'diana@digidyn.com', '555-0105', '987 Maple Dr', 'Philadelphia', 'USA'),
('Enterprise Group', 'Edward Norton', 'edward@entgroup.com', '555-0106', '147 Birch Blvd', 'San Antonio', 'USA'),
('Strategic Partners', 'Fiona Green', 'fiona@strategic.com', '555-0107', '258 Walnut Way', 'San Diego', 'USA'),
('Nexus Technologies', 'George Harris', 'george@nexustech.com', '555-0108', '369 Spruce St', 'Dallas', 'USA'),
('Quantum Systems', 'Helen Martin', 'helen@quantum.com', '555-0109', '741 Ash Ave', 'San Jose', 'USA'),
('Alpha Enterprises', 'Ivan Peterson', 'ivan@alpha.com', '555-0110', '852 Hickory Ln', 'Austin', 'USA'),
('Beta Solutions', 'Julia Roberts', 'julia@beta.com', '555-0111', '963 Poplar Rd', 'Jacksonville', 'USA'),
('Omega Corp', 'Kyle Anderson', 'kyle@omega.com', '555-0112', '159 Willow Dr', 'San Francisco', 'USA'),
('Delta Industries', 'Laura Wilson', 'laura@delta.com', '555-0113', '357 Sycamore Blvd', 'Columbus', 'USA'),
('Sigma Group', 'Mike Thompson', 'mike@sigma.com', '555-0114', '468 Chestnut Way', 'Charlotte', 'USA');

-- Insert Products
INSERT INTO dbo.Products (ProductName, Category, UnitPrice, UnitsInStock) VALUES
('Enterprise Software Suite', 'Software', 5000.00, 100),
('Cloud Storage Plan - Basic', 'Cloud Services', 99.00, 999),
('Cloud Storage Plan - Pro', 'Cloud Services', 299.00, 999),
('Cloud Storage Plan - Enterprise', 'Cloud Services', 999.00, 999),
('Security Assessment Package', 'Services', 2500.00, 50),
('Managed IT Support - Monthly', 'Services', 1500.00, 100),
('Custom Development - Per Hour', 'Services', 150.00, 500),
('Training Workshop - Full Day', 'Training', 800.00, 200),
('Certification Course', 'Training', 1200.00, 150),
('Hardware Server - Standard', 'Hardware', 8000.00, 25),
('Hardware Server - Premium', 'Hardware', 15000.00, 15),
('Network Equipment Bundle', 'Hardware', 3500.00, 40),
('Data Analytics Platform', 'Software', 12000.00, 75),
('AI/ML Toolkit License', 'Software', 8500.00, 60),
('API Integration Package', 'Software', 3000.00, 120);

-- Insert Sales Orders (sample orders across different dates)
INSERT INTO dbo.SalesOrders (CustomerId, ProductId, EmployeeId, OrderDate, Quantity, UnitPrice, Discount, Status) VALUES
-- January 2024
(1, 1, 7, '2024-01-05', 2, 5000.00, 10, 'Completed'),
(2, 3, 8, '2024-01-12', 5, 299.00, 0, 'Completed'),
(3, 6, 7, '2024-01-18', 12, 1500.00, 5, 'Completed'),
(4, 10, 9, '2024-01-25', 1, 8000.00, 0, 'Completed'),
-- February 2024
(5, 13, 7, '2024-02-02', 1, 12000.00, 15, 'Completed'),
(6, 5, 8, '2024-02-10', 3, 2500.00, 0, 'Completed'),
(7, 8, 9, '2024-02-15', 10, 800.00, 10, 'Completed'),
(8, 14, 7, '2024-02-22', 2, 8500.00, 5, 'Completed'),
-- March 2024
(9, 2, 8, '2024-03-01', 20, 99.00, 0, 'Completed'),
(10, 11, 7, '2024-03-08', 1, 15000.00, 10, 'Completed'),
(11, 7, 9, '2024-03-15', 40, 150.00, 0, 'Completed'),
(12, 4, 8, '2024-03-22', 3, 999.00, 5, 'Completed'),
-- April 2024
(13, 15, 7, '2024-04-05', 4, 3000.00, 0, 'Completed'),
(14, 9, 8, '2024-04-12', 8, 1200.00, 10, 'Completed'),
(15, 12, 9, '2024-04-20', 2, 3500.00, 0, 'Completed'),
(1, 6, 7, '2024-04-28', 6, 1500.00, 5, 'Completed'),
-- May 2024
(2, 1, 8, '2024-05-03', 1, 5000.00, 0, 'Completed'),
(3, 13, 7, '2024-05-10', 2, 12000.00, 10, 'Completed'),
(4, 3, 9, '2024-05-18', 10, 299.00, 5, 'Completed'),
(5, 10, 8, '2024-05-25', 2, 8000.00, 0, 'Completed'),
-- June 2024
(6, 14, 7, '2024-06-02', 1, 8500.00, 0, 'Completed'),
(7, 5, 9, '2024-06-10', 2, 2500.00, 5, 'Completed'),
(8, 8, 8, '2024-06-15', 15, 800.00, 10, 'Shipped'),
(9, 15, 7, '2024-06-22', 3, 3000.00, 0, 'Shipped'),
-- July 2024
(10, 7, 9, '2024-07-01', 25, 150.00, 0, 'Shipped'),
(11, 4, 8, '2024-07-08', 5, 999.00, 10, 'Processing'),
(12, 11, 7, '2024-07-15', 1, 15000.00, 5, 'Processing'),
(13, 2, 9, '2024-07-20', 30, 99.00, 0, 'Pending'),
(14, 6, 8, '2024-07-25', 3, 1500.00, 0, 'Pending'),
(15, 9, 7, '2024-07-28', 5, 1200.00, 5, 'Pending');
GO

-- =====================================================
-- PART 3: Create Views for AI Querying
-- =====================================================

-- Drop existing views if they exist
IF OBJECT_ID('dbo.vw_EmployeeDirectory', 'V') IS NOT NULL DROP VIEW dbo.vw_EmployeeDirectory;
IF OBJECT_ID('dbo.vw_DepartmentSummary', 'V') IS NOT NULL DROP VIEW dbo.vw_DepartmentSummary;
IF OBJECT_ID('dbo.vw_ProjectStatus', 'V') IS NOT NULL DROP VIEW dbo.vw_ProjectStatus;
IF OBJECT_ID('dbo.vw_SalesAnalytics', 'V') IS NOT NULL DROP VIEW dbo.vw_SalesAnalytics;
IF OBJECT_ID('dbo.vw_CustomerOrders', 'V') IS NOT NULL DROP VIEW dbo.vw_CustomerOrders;
IF OBJECT_ID('dbo.vw_ProductPerformance', 'V') IS NOT NULL DROP VIEW dbo.vw_ProductPerformance;
IF OBJECT_ID('dbo.vw_EmployeeProjects', 'V') IS NOT NULL DROP VIEW dbo.vw_EmployeeProjects;
IF OBJECT_ID('dbo.vw_MonthlySalesReport', 'V') IS NOT NULL DROP VIEW dbo.vw_MonthlySalesReport;
GO

-- View 1: Employee Directory
-- Use: "Who works in Engineering?" or "List all employees"
CREATE VIEW dbo.vw_EmployeeDirectory AS
SELECT
    e.EmployeeId,
    e.FirstName,
    e.LastName,
    e.FirstName + ' ' + e.LastName AS FullName,
    e.Email,
    e.JobTitle,
    d.DepartmentName,
    e.Salary,
    e.HireDate,
    DATEDIFF(YEAR, e.HireDate, GETDATE()) AS YearsWithCompany,
    CASE WHEN e.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
FROM dbo.Employees e
INNER JOIN dbo.Departments d ON e.DepartmentId = d.DepartmentId;
GO

-- View 2: Department Summary
-- Use: "What is the budget for each department?" or "How many employees per department?"
CREATE VIEW dbo.vw_DepartmentSummary AS
SELECT
    d.DepartmentId,
    d.DepartmentName,
    d.Location,
    d.Budget,
    COUNT(e.EmployeeId) AS EmployeeCount,
    AVG(e.Salary) AS AverageSalary,
    SUM(e.Salary) AS TotalSalaryExpense,
    d.Budget - ISNULL(SUM(e.Salary), 0) AS RemainingBudget
FROM dbo.Departments d
LEFT JOIN dbo.Employees e ON d.DepartmentId = e.DepartmentId
GROUP BY d.DepartmentId, d.DepartmentName, d.Location, d.Budget;
GO

-- View 3: Project Status
-- Use: "What projects are active?" or "Show me project budgets"
CREATE VIEW dbo.vw_ProjectStatus AS
SELECT
    p.ProjectId,
    p.ProjectName,
    p.Description,
    d.DepartmentName AS OwningDepartment,
    p.StartDate,
    p.EndDate,
    p.Budget,
    p.Status,
    COUNT(pa.AssignmentId) AS TeamSize,
    SUM(pa.HoursAllocated) AS TotalHoursAllocated,
    CASE
        WHEN p.EndDate < GETDATE() AND p.Status != 'Completed' THEN 'Overdue'
        WHEN DATEDIFF(DAY, GETDATE(), p.EndDate) <= 30 THEN 'Due Soon'
        ELSE 'On Track'
    END AS Timeline
FROM dbo.Projects p
INNER JOIN dbo.Departments d ON p.DepartmentId = d.DepartmentId
LEFT JOIN dbo.ProjectAssignments pa ON p.ProjectId = pa.ProjectId
GROUP BY p.ProjectId, p.ProjectName, p.Description, d.DepartmentName,
         p.StartDate, p.EndDate, p.Budget, p.Status;
GO

-- View 4: Sales Analytics
-- Use: "What are total sales?" or "Show sales by employee"
CREATE VIEW dbo.vw_SalesAnalytics AS
SELECT
    so.OrderId,
    c.CompanyName AS CustomerName,
    c.City AS CustomerCity,
    c.Country AS CustomerCountry,
    p.ProductName,
    p.Category AS ProductCategory,
    e.FirstName + ' ' + e.LastName AS SalesRepresentative,
    so.OrderDate,
    DATENAME(MONTH, so.OrderDate) AS OrderMonth,
    YEAR(so.OrderDate) AS OrderYear,
    so.Quantity,
    so.UnitPrice,
    so.Discount,
    so.TotalAmount,
    so.Status AS OrderStatus
FROM dbo.SalesOrders so
INNER JOIN dbo.Customers c ON so.CustomerId = c.CustomerId
INNER JOIN dbo.Products p ON so.ProductId = p.ProductId
INNER JOIN dbo.Employees e ON so.EmployeeId = e.EmployeeId;
GO

-- View 5: Customer Orders Summary
-- Use: "Who are our top customers?" or "Show customer order history"
CREATE VIEW dbo.vw_CustomerOrders AS
SELECT
    c.CustomerId,
    c.CompanyName,
    c.ContactName,
    c.ContactEmail,
    c.City,
    c.Country,
    COUNT(so.OrderId) AS TotalOrders,
    SUM(so.TotalAmount) AS TotalSpent,
    AVG(so.TotalAmount) AS AverageOrderValue,
    MIN(so.OrderDate) AS FirstOrderDate,
    MAX(so.OrderDate) AS LastOrderDate,
    DATEDIFF(DAY, MAX(so.OrderDate), GETDATE()) AS DaysSinceLastOrder
FROM dbo.Customers c
LEFT JOIN dbo.SalesOrders so ON c.CustomerId = so.CustomerId
GROUP BY c.CustomerId, c.CompanyName, c.ContactName, c.ContactEmail, c.City, c.Country;
GO

-- View 6: Product Performance
-- Use: "What products sell the most?" or "Show product revenue"
CREATE VIEW dbo.vw_ProductPerformance AS
SELECT
    p.ProductId,
    p.ProductName,
    p.Category,
    p.UnitPrice AS ListPrice,
    p.UnitsInStock,
    COUNT(so.OrderId) AS TotalOrderCount,
    SUM(so.Quantity) AS TotalUnitsSold,
    SUM(so.TotalAmount) AS TotalRevenue,
    AVG(so.Discount) AS AverageDiscount,
    CASE WHEN p.IsDiscontinued = 1 THEN 'Discontinued' ELSE 'Active' END AS ProductStatus
FROM dbo.Products p
LEFT JOIN dbo.SalesOrders so ON p.ProductId = so.ProductId
GROUP BY p.ProductId, p.ProductName, p.Category, p.UnitPrice, p.UnitsInStock, p.IsDiscontinued;
GO

-- View 7: Employee Projects
-- Use: "What projects is John working on?" or "Show project assignments"
CREATE VIEW dbo.vw_EmployeeProjects AS
SELECT
    e.EmployeeId,
    e.FirstName + ' ' + e.LastName AS EmployeeName,
    e.JobTitle,
    d.DepartmentName AS EmployeeDepartment,
    p.ProjectName,
    p.Status AS ProjectStatus,
    pa.Role AS ProjectRole,
    pa.HoursAllocated,
    pa.AssignedDate,
    p.StartDate AS ProjectStartDate,
    p.EndDate AS ProjectEndDate
FROM dbo.Employees e
INNER JOIN dbo.Departments d ON e.DepartmentId = d.DepartmentId
INNER JOIN dbo.ProjectAssignments pa ON e.EmployeeId = pa.EmployeeId
INNER JOIN dbo.Projects p ON pa.ProjectId = p.ProjectId;
GO

-- View 8: Monthly Sales Report
-- Use: "What were sales last month?" or "Show monthly revenue trends"
CREATE VIEW dbo.vw_MonthlySalesReport AS
SELECT
    YEAR(so.OrderDate) AS Year,
    MONTH(so.OrderDate) AS MonthNumber,
    DATENAME(MONTH, so.OrderDate) AS MonthName,
    COUNT(so.OrderId) AS OrderCount,
    SUM(so.Quantity) AS TotalUnitsSold,
    SUM(so.TotalAmount) AS TotalRevenue,
    AVG(so.TotalAmount) AS AverageOrderValue,
    COUNT(DISTINCT so.CustomerId) AS UniqueCustomers,
    COUNT(DISTINCT so.ProductId) AS UniqueProducts
FROM dbo.SalesOrders so
GROUP BY YEAR(so.OrderDate), MONTH(so.OrderDate), DATENAME(MONTH, so.OrderDate);
GO

-- =====================================================
-- Verification: Show what was created
-- =====================================================

PRINT '============================================='
PRINT 'Test Data Created Successfully!'
PRINT '============================================='
PRINT ''
PRINT 'TABLES CREATED:'
PRINT '  - dbo.Departments (8 records)'
PRINT '  - dbo.Employees (25 records)'
PRINT '  - dbo.Projects (8 records)'
PRINT '  - dbo.ProjectAssignments (22 records)'
PRINT '  - dbo.Customers (15 records)'
PRINT '  - dbo.Products (15 records)'
PRINT '  - dbo.SalesOrders (30 records)'
PRINT ''
PRINT 'VIEWS CREATED:'
PRINT '  - vw_EmployeeDirectory - Employee info with departments'
PRINT '  - vw_DepartmentSummary - Department stats and budgets'
PRINT '  - vw_ProjectStatus - Project details and timelines'
PRINT '  - vw_SalesAnalytics - Detailed sales transactions'
PRINT '  - vw_CustomerOrders - Customer purchase summaries'
PRINT '  - vw_ProductPerformance - Product sales metrics'
PRINT '  - vw_EmployeeProjects - Employee project assignments'
PRINT '  - vw_MonthlySalesReport - Monthly sales aggregations'
PRINT ''
PRINT 'Sample Queries to Test:'
PRINT '  SELECT * FROM vw_EmployeeDirectory WHERE DepartmentName = ''Engineering'''
PRINT '  SELECT * FROM vw_MonthlySalesReport ORDER BY Year, MonthNumber'
PRINT '  SELECT * FROM vw_CustomerOrders ORDER BY TotalSpent DESC'
GO
