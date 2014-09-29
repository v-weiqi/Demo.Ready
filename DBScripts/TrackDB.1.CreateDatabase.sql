-- Copyright (c) Microsoft Corporation.  All rights reserved.
Use Master
Go
IF EXISTS (SELECT * 
	   FROM   master..sysdatabases 
	   WHERE  name = N'TrackingWorkflow')
	DROP DATABASE TrackingWorkflow
GO
CREATE DATABASE TrackingWorkflow
GO
