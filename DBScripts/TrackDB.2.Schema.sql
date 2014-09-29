-- Copyright (c) Microsoft Corporation.  All rights reserved.

--==========================================================================
-- Sample Tracking Database
--==========================================================================

use [TrackingWorkflow]
go

set ansi_nulls on;
go

set quoted_identifier on;
go

if not exists (select * from sys.schemas where name = N'Microsoft.Samples.Tracking')
	exec ('create schema [Microsoft.Samples.Tracking]')
go


create table [Microsoft.Samples.Tracking].[WorkflowInstanceEventsTable]
(
	[Id] int identity(1,1) not null,
	[WorkflowInstanceId] uniqueidentifier not null,
	[WorkflowActivityDefinition] nvarchar(256) null,
	[RecordNumber] bigint not null,
	[State] nvarchar(128) null,
	[TraceLevelId] tinyint null,
	[Reason] nvarchar(2048) null,
	[ExceptionDetails] nvarchar (max) null,
	[SerializedAnnotations] nvarchar(max) null,
	[TimeCreated] [datetime] not null,
	constraint [PK_WorkflowInstanceEventsTable_Id] primary key ([Id]),
);

go


CREATE TABLE [dbo].[FailReason](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[Reason] [nchar](40) NULL,
 CONSTRAINT [PK_FailReason] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [dbo].[AppWorkFlowInstanceId](
	[AppId] [nchar](50) NULL,
	[AppVersion] [nchar](50) NULL,
	[WorkflowInstanceId] [uniqueidentifier] NOT NULL,
	[TimeCreated] [datetime] NULL,
	[TransactionId] [nchar](50) NULL,
	[Failed] [bit] NULL,
	[FailReason] [int] NULL,
	[AssignedTo] [text] NULL,
 CONSTRAINT [PK_AppWorkFlowInstanceId] PRIMARY KEY CLUSTERED 
(
	[WorkflowInstanceId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[AppWorkFlowInstanceId]  WITH NOCHECK ADD  CONSTRAINT [FK_AppWorkFlowInstanceId_FailReason] FOREIGN KEY([FailReason])
REFERENCES [dbo].[FailReason] ([id])
GO

ALTER TABLE [dbo].[AppWorkFlowInstanceId] CHECK CONSTRAINT [FK_AppWorkFlowInstanceId_FailReason]
GO

CREATE TABLE [dbo].[Vendors](
	[id] [smallint] NOT NULL,
	[name] [nvarchar](50) NULL,
	[alias] [nvarchar](10) NULL,
 CONSTRAINT [PK_Vendors] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

INSERT [dbo].[Vendors] ([id], [name], [alias]) VALUES (1, N'None', N'None')
INSERT [dbo].[Vendors] ([id], [name], [alias]) VALUES (2, N'Gang Wang', N'v-gaw')
INSERT [dbo].[Vendors] ([id], [name], [alias]) VALUES (3, N'Wei Q', N'v-weiqi')
INSERT [dbo].[Vendors] ([id], [name], [alias]) VALUES (4, N'Yoyo Yao', N'v-yoyao')


CREATE TABLE [dbo].[Comments](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[alias] [nchar](20) NULL,
	[comment] [nvarchar](max) NULL,
	[datecreated] [datetime] NULL,
	[transactionid] [nchar](50) NULL
) ON [PRIMARY]

GO


INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on Azure');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on Katal');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on IIS 7');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on IIS 7.5');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on IIS 8');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on IIS Express');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App Runtime failed on IIS 6');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Invalid Package');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Publish to Remote server Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Publish to Azure site Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Publish to Katal Site Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Download from Remote server Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Download from Azure site Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('Download to form Site Failed');
INSERT INTO [TrackingWorkflow].[dbo].[FailReason] ([Reason]) VALUES ('App detection Failed in Web Matrix');

CREATE TABLE [dbo].[WorkFlowType](
	[WorkFlowId] [int] NOT NULL,
	[Type] [nchar](50) NULL,
 CONSTRAINT [PK_WorkFlowType] PRIMARY KEY CLUSTERED 
(
	[WorkFlowId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowType]([WorkFlowId],[Type]) VALUES (1, 'Azure');

CREATE TABLE [dbo].[WorkFlowStep](
	[StepId] [int] NOT NULL,
	[Name] [nchar](50) NULL,
	[DisplayName] [nchar](200) NULL,
 CONSTRAINT [PK_WorkFlowStep] PRIMARY KEY CLUSTERED 
(
	[StepId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('1','GenerateAndValidate','Generate & Validate');
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('2','ContinueToTC2','Continue to TC2');
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('3','VendorTC2Response','Vendor Response');
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('4','ContinueToProd','Continue to Production');
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('5','BackupFeedAndPackages','Backup Feed And Packages');
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowStep]([StepId],[Name],[DisplayName])VALUES('6','PublishToProd','Publish to Production');

CREATE TABLE [dbo].[WorkFlowTypeStep](
	[WorkFlowId] [int] NOT NULL,
	[StepId] [int] NOT NULL,
	[Rank] [int] NOT NULL,
 CONSTRAINT [PK_WorkFlowTypeStep] PRIMARY KEY CLUSTERED 
(
	[WorkFlowId] ASC,
	[StepId] ASC,
	[Rank] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[WorkFlowTypeStep]  WITH CHECK ADD  CONSTRAINT [FK_WorkFlowTypeStep_WorkFlowStep] FOREIGN KEY([StepId])
REFERENCES [dbo].[WorkFlowStep] ([StepId])
GO

ALTER TABLE [dbo].[WorkFlowTypeStep] CHECK CONSTRAINT [FK_WorkFlowTypeStep_WorkFlowStep]
GO

ALTER TABLE [dbo].[WorkFlowTypeStep]  WITH CHECK ADD  CONSTRAINT [FK_WorkFlowTypeStep_WorkFlowType] FOREIGN KEY([WorkFlowId])
REFERENCES [dbo].[WorkFlowType] ([WorkFlowId])
GO

ALTER TABLE [dbo].[WorkFlowTypeStep] CHECK CONSTRAINT [FK_WorkFlowTypeStep_WorkFlowType]
GO

INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowTypeStep] ([WorkFlowId] ,[StepId] ,[Rank]) VALUES (1 ,1 ,1);
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowTypeStep] ([WorkFlowId] ,[StepId] ,[Rank]) VALUES (1 ,2 ,2);
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowTypeStep] ([WorkFlowId] ,[StepId] ,[Rank]) VALUES (1 ,3 ,3);
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowTypeStep] ([WorkFlowId] ,[StepId] ,[Rank]) VALUES (1 ,4 ,4);
INSERT INTO [TrackingWorkflow].[dbo].[WorkFlowTypeStep] ([WorkFlowId] ,[StepId] ,[Rank]) VALUES (1 ,5 ,5);

CREATE TABLE [dbo].[Status](
	[transactionid] [nchar](50) NULL,
	[StepId] [int] NOT NULL,
	[status] [nvarchar](255) NULL,
	[passed] [bit] NULL,
	[Log] [nvarchar](max) NULL,
	[datecreated] [datetime] NULL
) ON [PRIMARY]
GO

create table [Microsoft.Samples.Tracking].[ActivityInstanceEventsTable]
(
	[Id] int identity(1,1) not null,
	[WorkflowInstanceId] uniqueidentifier not null,
	[RecordNumber] bigint not null,
	[State] nvarchar(128) null,
	[TraceLevelId] tinyint null,
	[ActivityRecordType] nvarchar(128) not null,
	[ActivityName] nvarchar(1024) null,
	[ActivityId] nvarchar(256) null,
	[ActivityInstanceId] nvarchar (256) null,
	[ActivityType] nvarchar(2048) null,
	[SerializedArguments] nvarchar(max) null,
	[SerializedVariables] nvarchar(max) null,
        [SerializedAnnotations] nvarchar(max) null,
	[TimeCreated] datetime not null,
	
	constraint [PK_ActivityInstanceEventsTable_Id] primary key ([Id]),
);

go

create table [Microsoft.Samples.Tracking].[ExtendedActivityEventsTable]
(
	[Id] int identity(1,1) not null,
	[WorkflowInstanceId] uniqueidentifier not null,
	[RecordNumber] bigint null,
	[TraceLevelId] tinyint null,
	[ActivityRecordType] nvarchar(128) not null,
	[ActivityName] nvarchar(1024) null,
	[ActivityId] nvarchar(256) null,
	[ActivityInstanceId] nvarchar (256) null,
	[ActivityType] nvarchar(2048) null,
	[ChildActivityName] nvarchar(1024) null,
	[ChildActivityId] nvarchar(256) null,
	[ChildActivityInstanceId] nvarchar (256) null,
	[ChildActivityType] nvarchar(2048) null,
	[FaultDetails]	    nvarchar(max) null,
	[FaultHandlerActivityName] nvarchar(1024) null,
	[FaultHandlerActivityId] nvarchar(256) null,
	[FaultHandlerActivityInstanceId] nvarchar (256) null,
	[FaultHandlerActivityType] nvarchar(2048) null,
	[SerializedAnnotations] nvarchar(max) null,
	[TimeCreated] datetime not null,
	
	constraint [PK_ExtendedActivityInstanceEventsTable_Id] primary key ([Id]),
);

go

create table [Microsoft.Samples.Tracking].[BookmarkResumptionEventsTable]
( 
    [Id] int identity(1,1) not null,
    [WorkflowInstanceId] uniqueidentifier not null,
    [RecordNumber] bigint null,
    [TraceLevelId] tinyint null,
    [BookmarkName] nvarchar(1024),
    [BookmarkScope] uniqueidentifier null,
    [OwnerActivityName] nvarchar(256) null,
    [OwnerActivityId] nvarchar(256) null,
    [OwnerActivityInstanceId] nvarchar(256) null,
    [OwnerActivityType] nvarchar(256) null,
    [SerializedAnnotations] nvarchar(max) null,
    [TimeCreated] datetime not null,

    constraint [PK_BookmarkResumptionkEventsTable_Id] primary key ([Id])
);
go

create table [Microsoft.Samples.Tracking].[CustomTrackingEventsTable]
(
	[Id] int identity(1,1) not null,
	[WorkflowInstanceId] uniqueidentifier not null,
	[RecordNumber] bigint null,
	[TraceLevelId] tinyint null,
	[CustomRecordName] nvarchar(2048) null,
	[ActivityName] nvarchar(2048) null,
	[ActivityId] nvarchar (256) null,
	[ActivityInstanceId] nvarchar (256) null,
	[ActivityType] nvarchar (256) null,
	[SerializedData] nvarchar(max) null,
	[SerializedAnnotations] nvarchar(max) null,
	[TimeCreated] [datetime] not null,

	constraint [PK_CustomTrackingEventsTable_Id] primary key ([Id]),
);

go

create procedure [Microsoft.Samples.Tracking].[InsertWorkflowInstanceEvent]	
(	
													 @WorkflowInstanceId				uniqueidentifier	
													,@WorkflowActivityDefinition		nvarchar(256)
													,@RecordNumber					bigint
													,@State						nvarchar(128)
													,@TraceLevelId                      	tinyint
													,@Reason						nvarchar(max)
													,@ExceptionDetails				nvarchar(max)
													,@AnnotationsXml				nvarchar(2048)
													,@TimeCreated                       datetime
																			
)
as
 begin
	set nocount on


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret			smallint


	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[WorkflowInstanceEventsTable] (
				[WorkflowInstanceId]
				,[WorkflowActivityDefinition]
				,[RecordNumber]
				,[State]
				,[TraceLevelId]
				,[ExceptionDetails]
				,[Reason]
				,[SerializedAnnotations] 
				,[TimeCreated]
		) values (
				@WorkflowInstanceId
				,@WorkflowActivityDefinition
				,@RecordNumber
				,@State
				,@TraceLevelId
				,@ExceptionDetails
				,@Reason
				,@AnnotationsXml
				,@TimeCreated
				
		)
		if (@@ROWCOUNT <> 1)
        begin
         --   exec @errorMsg=[System.Globalization].[SessionsString] '4CED8A16-944D-4340-BC6A-09E7C3AC2ADF';
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

 end
go

create procedure [Microsoft.Samples.Tracking].[InsertActivityInstanceEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@State                nvarchar(128)
													 ,@TraceLevelId         tinyint
	                                                 						 ,@ActivityRecordType   nvarchar(128)
	                                                 						 ,@ActivityName         nvarchar(1024)
													 ,@ActivityId           nvarchar(256)
													 ,@ActivityInstanceId   nvarchar (256)
													 ,@ActivityType         nvarchar (2048)
													 ,@ArgumentsXml	        nvarchar(max)
													 ,@VariablesXml	        nvarchar(max)
													 ,@AnnotationsXml       nvarchar(max)
													 ,@TimeCreated          datetime												 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[ActivityInstanceEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[State]
				,[TraceLevelId]
				,[ActivityRecordType] 
	            		,[ActivityName] 
	            		,[ActivityId] 
	            		,[ActivityInstanceId]
	            		,[ActivityType]
                    		,[SerializedArguments]
	            		,[SerializedVariables]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber         
				,@State                
				,@TraceLevelId         
	            		,@ActivityRecordType   
	            		,@ActivityName         
	            		,@ActivityId           
			    	,@ActivityInstanceId   
			    	,@ActivityType
			    	,@ArgumentsXml
			    	,@VariablesXml
			    	,@AnnotationsXml  
			    	,@TimeCreated 
			       
				
		)
		
		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

 end
go


create procedure [Microsoft.Samples.Tracking].[InsertActivityScheduledEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@TraceLevelId         tinyint
	                                                 						 ,@ActivityRecordType   nvarchar(128)
	                                                 						 ,@ActivityName         nvarchar(1024)
													 ,@ActivityId           nvarchar(256)
													 ,@ActivityInstanceId   nvarchar (256)
													 ,@ActivityType         nvarchar (2048)
													 ,@ChildActivityName         nvarchar(1024)
													 ,@ChildActivityId           nvarchar(256)
													 ,@ChildActivityInstanceId   nvarchar (256)
													 ,@ChildActivityType         nvarchar (2048)
													 ,@AnnotationsXml			nvarchar(max)
													 ,@TimeCreated          datetime
													 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[ExtendedActivityEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[TraceLevelId]
				,[ActivityRecordType] 
	            		,[ActivityName] 
	            		,[ActivityId] 
	            		,[ActivityInstanceId]
	            		,[ActivityType]
				,[ChildActivityName] 
	            		,[ChildActivityId] 
	            		,[ChildActivityInstanceId]
	            		,[ChildActivityType]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber                 
				,@TraceLevelId         
	            		,@ActivityRecordType   
	            		,@ActivityName         
	            		,@ActivityId           
			    	,@ActivityInstanceId   
			    	,@ActivityType
			    	,@ChildActivityName         
	            		,@ChildActivityId          
			    	,@ChildActivityInstanceId   
			    	,@ChildActivityType
			    	,@AnnotationsXml     
				,@TimeCreated 
				       
				
		)
		
		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

 end
go

create procedure [Microsoft.Samples.Tracking].[InsertActivityCancelRequestedEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@TraceLevelId         tinyint
	                                                 						 ,@ActivityRecordType   nvarchar(128)
	                                                 						 ,@ActivityName         nvarchar(1024)
													 ,@ActivityId           nvarchar(256)
													 ,@ActivityInstanceId   nvarchar (256)
													 ,@ActivityType         nvarchar (2048)
													 ,@ChildActivityName         nvarchar(1024)
													 ,@ChildActivityId           nvarchar(256)
													 ,@ChildActivityInstanceId   nvarchar (256)
													 ,@ChildActivityType         nvarchar (2048)
													 ,@AnnotationsXml nvarchar(max)
													 ,@TimeCreated          datetime
													 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[ExtendedActivityEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[TraceLevelId]
				,[ActivityRecordType] 
	           	        ,[ActivityName] 
	            		,[ActivityId] 
	            		,[ActivityInstanceId]
	            		,[ActivityType]
				,[ChildActivityName] 
	            		,[ChildActivityId] 
	            		,[ChildActivityInstanceId]
	            		,[ChildActivityType]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber                 
				,@TraceLevelId         
	            		,@ActivityRecordType   
	            		,@ActivityName         
	            		,@ActivityId           
			    	,@ActivityInstanceId   
			    	,@ActivityType
			    	,@ChildActivityName         
	            		,@ChildActivityId          
			    	,@ChildActivityInstanceId   
			    	,@ChildActivityType
			    	,@AnnotationsXml    
				,@TimeCreated 
				       
				
		)
		
		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

 end
go

create procedure [Microsoft.Samples.Tracking].[InsertFaultPropagationEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@TraceLevelId         tinyint
	                                                 						 ,@ActivityRecordType   nvarchar(128)
	                                                						 ,@ActivityName         nvarchar(1024)
													 ,@ActivityId           nvarchar(256)
													 ,@ActivityInstanceId   nvarchar (256)
													 ,@ActivityType         nvarchar (2048)
													 ,@FaultDetails		nvarchar (max)
													 ,@FaultHandlerActivityName         nvarchar(1024)
													 ,@FaultHandlerActivityId           nvarchar(256)
													 ,@FaultHandlerActivityInstanceId   nvarchar (256)
													 ,@FaultHandlerActivityType         nvarchar (2048)
													 ,@AnnotationsXml nvarchar(max)
													 ,@TimeCreated          datetime
													 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[ExtendedActivityEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[TraceLevelId]
				,[ActivityRecordType] 
	            		,[ActivityName] 
	            		,[ActivityId] 
	            		,[ActivityInstanceId]
	            		,[ActivityType]
				,[FaultDetails]
				,[FaultHandlerActivityName] 
	            		,[FaultHandlerActivityId] 
	            		,[FaultHandlerActivityInstanceId]
	            		,[FaultHandlerActivityType]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber                 
				,@TraceLevelId         
	            		,@ActivityRecordType   
	            		,@ActivityName         
	            		,@ActivityId           
			    	,@ActivityInstanceId   
			    	,@ActivityType
				,@FaultDetails
			    	,@FaultHandlerActivityName         
	            		,@FaultHandlerActivityId          
			    	,@FaultHandlerActivityInstanceId   
			    	,@FaultHandlerActivityType
			    	,@AnnotationsXml    
				,@TimeCreated 
				       
				
		)
		
		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

 end
go

create procedure [Microsoft.Samples.Tracking].[InsertBookmarkResumptionEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@TraceLevelId         tinyint
													 ,@BookmarkName         nvarchar(1024)
													 ,@BookmarkScope        uniqueidentifier
	                                                 						 ,@OwnerActivityName         nvarchar(1024)
													 ,@OwnerActivityId           nvarchar(256)
													 ,@OwnerActivityInstanceId   nvarchar (256)
													 ,@OwnerActivityType         nvarchar (2048)
													 ,@AnnotationsXml       nvarchar(max)
													 ,@TimeCreated          datetime
													 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[BookmarkResumptionEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[TraceLevelId]
				,[BookmarkName]
				,[BookmarkScope]
	            		,[OwnerActivityName] 
	            		,[OwnerActivityId] 
	            		,[OwnerActivityInstanceId]
	            		,[OwnerActivityType]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber                        
				,@TraceLevelId         
	            		,@BookmarkName
	            		,@BookmarkScope
	            		,@OwnerActivityName         
	            		,@OwnerActivityId           
			    	,@OwnerActivityInstanceId   
			    	,@OwnerActivityType
			    	,@AnnotationsXml
				,@TimeCreated 
				       
				
		)

		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

end

go

create procedure [Microsoft.Samples.Tracking].[InsertCustomTrackingEvent]	
(	
													 @WorkflowInstanceId	uniqueidentifier
													 ,@RecordNumber         bigint
													 ,@TraceLevelId         tinyint
													 ,@CustomRecordName     nvarchar(1024)
	                                                 						 ,@ActivityName         nvarchar(1024)
													 ,@ActivityId           nvarchar(256)
													 ,@ActivityInstanceId   nvarchar (256)
													 ,@ActivityType         nvarchar (2048)
													 ,@CustomRecordDataXml  nvarchar(max)
													 ,@AnnotationsXml       nvarchar(max)
													 ,@TimeCreated          datetime
													 
																			
)
as
 begin
	set NOCOUNT ON


	declare @local_tran		bit
			,@error			int
			,@errorMsg	nvarchar(256)
			,@ret		int



	if @@TRANCOUNT > 0
		set @local_tran = 0
	else
	 begin
		begin TRANSACTION
		set @local_tran = 1		
	 end
	
	select @error = @@ERROR

	begin try
	
		insert [Microsoft.Samples.Tracking].[CustomTrackingEventsTable] (
				[WorkflowInstanceId]
				,[RecordNumber]
				,[TraceLevelId]
				,[CustomRecordName]
	            		,[ActivityName] 
	            		,[ActivityId] 
	            		,[ActivityInstanceId]
	            		,[ActivityType]
	            		,[SerializedData]
	            		,[SerializedAnnotations]
				,[TimeCreated]
		) values (
				 @WorkflowInstanceId
	            		,@RecordNumber                        
				,@TraceLevelId         
	            		,@CustomRecordName
	            		,@ActivityName         
	            		,@ActivityId           
			    	,@ActivityInstanceId   
			    	,@ActivityType
			    	,@CustomRecordDataXml
			    	,@AnnotationsXml    
				,@TimeCreated 
				       
				)
				
		if (@@ROWCOUNT <> 1)
        begin
            raiserror (@errorMsg, 16, -1);
        end 
     end try		
	 begin catch
            set @errorMsg = error_message();
            goto failed;
     end catch;

	if @local_tran = 1
		COMMIT TRANSACTION

	select	@ret = 0

	goto done

failed:
	if @local_tran = 1
		ROLLBACK TRANSACTION

	RAISERROR( @errorMsg, 16, -1 )

	set @ret = -1
	goto done

done:
	return @ret

end

go