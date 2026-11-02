USE AirborneDB;
GO

SELECT 'dbo.EnquiryMaster' AS ObjectName,
       CASE WHEN OBJECT_ID('dbo.EnquiryMaster', 'U') IS NULL THEN 'MISSING' ELSE 'OK' END AS Status
UNION ALL
SELECT 'dbo.SP_ManageEnquiry',
       CASE WHEN OBJECT_ID('dbo.SP_ManageEnquiry', 'P') IS NULL THEN 'MISSING' ELSE 'OK' END
UNION ALL
SELECT 'dbo.ManageMedia',
       CASE WHEN OBJECT_ID('dbo.ManageMedia', 'P') IS NULL THEN 'MISSING' ELSE 'OK' END
UNION ALL
SELECT 'dbo.BlogMaster',
       CASE WHEN OBJECT_ID('dbo.BlogMaster', 'U') IS NULL THEN 'Run BlogDatabase.sql first' ELSE 'OK' END;
GO
