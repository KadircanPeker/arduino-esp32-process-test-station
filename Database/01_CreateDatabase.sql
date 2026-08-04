/*
  Arduino / ESP32 Process Test and Traceability Station
  Manual SQL Server setup reference.

  The desktop application normally creates this schema automatically.
  Run this script with an account allowed to create databases and tables.
*/

USE [master];
GO

IF DB_ID(N'ProcessTestDb') IS NULL
BEGIN
    CREATE DATABASE [ProcessTestDb];
END;
GO

USE [ProcessTestDb];
GO

IF OBJECT_ID(N'dbo.ProcessUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessUsers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(512) NOT NULL,
        FullName NVARCHAR(120) NOT NULL,
        Role NVARCHAR(40) NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );
END;
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL,
        ActionTime DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        ActionType NVARCHAR(80) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        OldValue NVARCHAR(1000) NULL,
        NewValue NVARCHAR(1000) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ProductThresholds', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductThresholds (
        ProductType NVARCHAR(100) NOT NULL PRIMARY KEY,
        MinVoltage DECIMAL(10,3) NOT NULL,
        MaxVoltage DECIMAL(10,3) NOT NULL,
        MinCurrent DECIMAL(10,3) NOT NULL,
        MaxCurrent DECIMAL(10,3) NOT NULL,
        IpcClass NVARCHAR(30) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ProcessTestLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessTestLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SerialNumber NVARCHAR(80) NOT NULL,
        ProductType NVARCHAR(100) NOT NULL,
        Voltage FLOAT NOT NULL,
        [Current] FLOAT NOT NULL,
        Result NVARCHAR(10) NOT NULL,
        ErrorCode NVARCHAR(30) NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        TestAttemptNo INT NOT NULL DEFAULT 1,
        StationName NVARCHAR(100) NULL,
        OperatorName NVARCHAR(120) NULL,
        SourceType NVARCHAR(40) NULL,
        BatchNo NVARCHAR(80) NULL
    );

    CREATE INDEX IX_ProcessTestLogs_CreatedDate
        ON dbo.ProcessTestLogs(CreatedDate DESC);

    CREATE INDEX IX_ProcessTestLogs_Result
        ON dbo.ProcessTestLogs(Result, ErrorCode);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ProductThresholds WHERE ProductType = N'VOLTAGE_RELAY_TESTER')
BEGIN
    INSERT INTO dbo.ProductThresholds
        (ProductType, MinVoltage, MaxVoltage, MinCurrent, MaxCurrent, IpcClass)
    VALUES
        (N'VOLTAGE_RELAY_TESTER', 1.000, 4.500, 0.000, 2.500, N'ARDUINO');
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ProductThresholds WHERE ProductType = N'WIFI_TESTER')
BEGIN
    INSERT INTO dbo.ProductThresholds
        (ProductType, MinVoltage, MaxVoltage, MinCurrent, MaxCurrent, IpcClass)
    VALUES
        (N'WIFI_TESTER', 0.000, 75.000, 0.000, 100.000, N'ESP32');
END;
GO
