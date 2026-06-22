-- ============================================================================
-- DOCVAULT MODULE 6: WORKFLOW & APPROVALS SCHEMA
-- ============================================================================

CREATE TABLE [dbo].[DocumentWorkflows] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [DocumentId] UNIQUEIDENTIFIER NOT NULL UNIQUE,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [WorkflowName] NVARCHAR(500) NOT NULL,
    [Status] INT NOT NULL DEFAULT 0, -- 0=Draft, 1=InReview, 2=PendingApproval, 3=Approved, 4=Rejected, 5=Published, 6=Archived
    [CurrentStageIndex] INT NOT NULL DEFAULT 0,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CompletedAt] DATETIME2 NULL,
    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_DocumentWorkflows_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]),
    CONSTRAINT FK_DocumentWorkflows_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_DocumentWorkflows_CreatedBy FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_DocumentWorkflows_Status CHECK ([Status] BETWEEN 0 AND 6)
);

CREATE INDEX IDX_DocumentWorkflows_Document ON [dbo].[DocumentWorkflows]([DocumentId]);
CREATE INDEX IDX_DocumentWorkflows_Organization ON [dbo].[DocumentWorkflows]([OrganizationId]);
CREATE INDEX IDX_DocumentWorkflows_Status ON [dbo].[DocumentWorkflows]([Status]);
CREATE INDEX IDX_DocumentWorkflows_CreatedAt ON [dbo].[DocumentWorkflows]([CreatedAt] DESC);

-- ============================================================================
-- WORKFLOW STAGES TABLE
-- ============================================================================

CREATE TABLE [dbo].[WorkflowStages] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [StageNumber] INT NOT NULL,

    [StageName] NVARCHAR(500) NOT NULL, -- Draft, Review, Approval, Publishing
    [Description] NVARCHAR(MAX) NULL,

    [ApprovalType] INT NOT NULL, -- 0=Sequential, 1=Parallel, 2=Optional
    [RequiredApprovals] INT NOT NULL DEFAULT 1,

    [Status] INT NOT NULL DEFAULT 0, -- 0=NotStarted, 1=InProgress, 2=Completed, 3=Skipped, 4=Rejected
    [StartedAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,

    [CanRejectToStart] BIT NOT NULL DEFAULT 0,
    [CanSkip] BIT NOT NULL DEFAULT 0,

    CONSTRAINT FK_WorkflowStages_Workflow FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[DocumentWorkflows]([Id]) ON DELETE CASCADE,
    CONSTRAINT UQ_WorkflowStages_StageNumber UNIQUE ([WorkflowId], [StageNumber]),
    CONSTRAINT CK_WorkflowStages_ApprovalType CHECK ([ApprovalType] BETWEEN 0 AND 2),
    CONSTRAINT CK_WorkflowStages_Status CHECK ([Status] BETWEEN 0 AND 4)
);

CREATE INDEX IDX_WorkflowStages_Workflow ON [dbo].[WorkflowStages]([WorkflowId]);
CREATE INDEX IDX_WorkflowStages_Status ON [dbo].[WorkflowStages]([Status]);

-- ============================================================================
-- APPROVAL TASKS TABLE
-- ============================================================================

CREATE TABLE [dbo].[ApprovalTasks] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [StageId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,

    [AssignedToUserId] UNIQUEIDENTIFIER NOT NULL,
    [Status] INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Rejected, 3=Skipped

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [DueDate] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,

    [Comments] NVARCHAR(MAX) NULL,
    [RejectionReason] NVARCHAR(MAX) NULL,

    [Priority] INT NOT NULL DEFAULT 0, -- 0=Normal, 1=High, 2=Urgent

    CONSTRAINT FK_ApprovalTasks_Workflow FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[DocumentWorkflows]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_ApprovalTasks_Stage FOREIGN KEY ([StageId]) REFERENCES [dbo].[WorkflowStages]([Id]),
    CONSTRAINT FK_ApprovalTasks_Document FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents]([Id]),
    CONSTRAINT FK_ApprovalTasks_AssignedTo FOREIGN KEY ([AssignedToUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_ApprovalTasks_Status CHECK ([Status] BETWEEN 0 AND 3)
);

CREATE INDEX IDX_ApprovalTasks_Workflow ON [dbo].[ApprovalTasks]([WorkflowId]);
CREATE INDEX IDX_ApprovalTasks_AssignedTo ON [dbo].[ApprovalTasks]([AssignedToUserId]);
CREATE INDEX IDX_ApprovalTasks_Status ON [dbo].[ApprovalTasks]([Status]);
CREATE INDEX IDX_ApprovalTasks_DueDate ON [dbo].[ApprovalTasks]([DueDate]);
CREATE INDEX IDX_ApprovalTasks_Priority ON [dbo].[ApprovalTasks]([Priority]) WHERE [Status] = 0;

-- ============================================================================
-- WORKFLOW NOTIFICATIONS TABLE
-- ============================================================================

CREATE TABLE [dbo].[WorkflowNotifications] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [RecipientUserId] UNIQUEIDENTIFIER NOT NULL,

    [Subject] NVARCHAR(500) NOT NULL,
    [Message] NVARCHAR(MAX) NOT NULL,
    [Type] INT NOT NULL, -- 0=TaskAssigned, 1=Approved, 2=Rejected, 3=Expired, 4=WorkflowCompleted

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IsRead] BIT NOT NULL DEFAULT 0,
    [ReadAt] DATETIME2 NULL,

    CONSTRAINT FK_WorkflowNotifications_Workflow FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[DocumentWorkflows]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_WorkflowNotifications_Recipient FOREIGN KEY ([RecipientUserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT CK_WorkflowNotifications_Type CHECK ([Type] BETWEEN 0 AND 4)
);

CREATE INDEX IDX_WorkflowNotifications_Recipient ON [dbo].[WorkflowNotifications]([RecipientUserId]);
CREATE INDEX IDX_WorkflowNotifications_IsRead ON [dbo].[WorkflowNotifications]([IsRead]) WHERE [IsRead] = 0;
CREATE INDEX IDX_WorkflowNotifications_CreatedAt ON [dbo].[WorkflowNotifications]([CreatedAt] DESC);

-- ============================================================================
-- WORKFLOW TEMPLATES TABLE
-- ============================================================================

CREATE TABLE [dbo].[WorkflowTemplates] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,

    [TemplateName] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,

    [IsActive] BIT NOT NULL DEFAULT 1,
    [IsDefault] BIT NOT NULL DEFAULT 0,

    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_WorkflowTemplates_Organization FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organizations]([Id]),
    CONSTRAINT FK_WorkflowTemplates_CreatedBy FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id])
);

CREATE INDEX IDX_WorkflowTemplates_Organization ON [dbo].[WorkflowTemplates]([OrganizationId]);
CREATE INDEX IDX_WorkflowTemplates_IsActive ON [dbo].[WorkflowTemplates]([IsActive]) WHERE [IsActive] = 1;

-- ============================================================================
-- WORKFLOW HISTORY TABLE (Audit)
-- ============================================================================

CREATE TABLE [dbo].[WorkflowHistory] (
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [StageId] UNIQUEIDENTIFIER NULL,

    [Action] NVARCHAR(50) NOT NULL, -- Created, Submitted, StageApproved, StageRejected, Completed, Rejected
    [ChangedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [ChangedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    [Details] NVARCHAR(MAX) NULL,

    CONSTRAINT FK_WorkflowHistory_Workflow FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[DocumentWorkflows]([Id]) ON DELETE CASCADE,
    CONSTRAINT FK_WorkflowHistory_ChangedBy FOREIGN KEY ([ChangedByUserId]) REFERENCES [dbo].[Users]([Id])
);

CREATE INDEX IDX_WorkflowHistory_Workflow ON [dbo].[WorkflowHistory]([WorkflowId]);
CREATE INDEX IDX_WorkflowHistory_Action ON [dbo].[WorkflowHistory]([Action]);
CREATE INDEX IDX_WorkflowHistory_ChangedAt ON [dbo].[WorkflowHistory]([ChangedAt] DESC);

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE PROCEDURE [dbo].[sp_GetPendingTasks]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        at.[Id],
        at.[WorkflowId],
        at.[StageId],
        at.[DocumentId],
        at.[Status],
        at.[DueDate],
        at.[Priority],
        ws.[StageName],
        d.[Title]
    FROM [dbo].[ApprovalTasks] at
    INNER JOIN [dbo].[WorkflowStages] ws ON at.[StageId] = ws.[Id]
    INNER JOIN [dbo].[Documents] d ON at.[DocumentId] = d.[Id]
    WHERE at.[AssignedToUserId] = @UserId
    AND at.[Status] = 0 -- Pending
    ORDER BY at.[Priority] DESC, at.[DueDate] ASC;
END;
GO

CREATE PROCEDURE [dbo].[sp_CompleteWorkflowStage]
    @WorkflowId UNIQUEIDENTIFIER,
    @StageId UNIQUEIDENTIFIER
AS
BEGIN
    DECLARE @NextStageNumber INT;
    DECLARE @MaxStageNumber INT;

    -- Mark current stage as completed
    UPDATE [dbo].[WorkflowStages]
    SET [Status] = 2, [CompletedAt] = GETUTCDATE()
    WHERE [Id] = @StageId;

    -- Get next stage
    SELECT @NextStageNumber = [StageNumber] + 1
    FROM [dbo].[WorkflowStages]
    WHERE [Id] = @StageId;

    SELECT @MaxStageNumber = MAX([StageNumber])
    FROM [dbo].[WorkflowStages]
    WHERE [WorkflowId] = @WorkflowId;

    IF @NextStageNumber <= @MaxStageNumber
    BEGIN
        -- Start next stage
        UPDATE [dbo].[WorkflowStages]
        SET [Status] = 1, [StartedAt] = GETUTCDATE()
        WHERE [WorkflowId] = @WorkflowId
        AND [StageNumber] = @NextStageNumber;
    END
    ELSE
    BEGIN
        -- Workflow complete
        UPDATE [dbo].[DocumentWorkflows]
        SET [Status] = 3, [CompletedAt] = GETUTCDATE() -- Approved
        WHERE [Id] = @WorkflowId;

        UPDATE [dbo].[Documents]
        SET [Status] = 3 -- Published
        WHERE [Id] = (SELECT [DocumentId] FROM [dbo].[DocumentWorkflows] WHERE [Id] = @WorkflowId);
    END
END;
GO

CREATE PROCEDURE [dbo].[sp_GetWorkflowHistory]
    @WorkflowId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        [Id],
        [WorkflowId],
        [StageId],
        [Action],
        [ChangedByUserId],
        [ChangedAt],
        [Details]
    FROM [dbo].[WorkflowHistory]
    WHERE [WorkflowId] = @WorkflowId
    ORDER BY [ChangedAt] DESC;
END;
GO

-- ============================================================================
-- VIEWS
-- ============================================================================

CREATE VIEW [dbo].[vw_OverdueApprovalTasks]
AS
SELECT
    at.[Id],
    at.[WorkflowId],
    d.[Title],
    at.[AssignedToUserId],
    at.[DueDate],
    DATEDIFF(DAY, at.[DueDate], GETUTCDATE()) AS DaysOverdue
FROM [dbo].[ApprovalTasks] at
INNER JOIN [dbo].[Documents] d ON at.[DocumentId] = d.[Id]
WHERE at.[Status] = 0 -- Pending
AND at.[DueDate] < GETUTCDATE();
GO

CREATE VIEW [dbo].[vw_WorkflowProgress]
AS
SELECT
    dw.[Id],
    dw.[DocumentId],
    d.[Title],
    dw.[Status],
    dw.[CurrentStageIndex],
    ws.[StageName],
    COUNT(at.[Id]) AS TotalApprovals,
    SUM(CASE WHEN at.[Status] = 1 THEN 1 ELSE 0 END) AS ApprovedCount
FROM [dbo].[DocumentWorkflows] dw
INNER JOIN [dbo].[Documents] d ON dw.[DocumentId] = d.[Id]
INNER JOIN [dbo].[WorkflowStages] ws ON dw.[Id] = ws.[WorkflowId]
LEFT JOIN [dbo].[ApprovalTasks] at ON ws.[Id] = at.[StageId]
GROUP BY dw.[Id], dw.[DocumentId], d.[Title], dw.[Status], dw.[CurrentStageIndex], ws.[StageName];
GO
