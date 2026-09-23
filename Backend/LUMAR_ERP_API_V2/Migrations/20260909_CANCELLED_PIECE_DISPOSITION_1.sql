SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.CancelledPieceDisposition', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CancelledPieceDisposition
    (
        CancelledPieceDispositionId int IDENTITY(1,1) NOT NULL,
        PieceId int NOT NULL,
        Decision nvarchar(80) NOT NULL,
        Reason nvarchar(500) NULL,
        DecidedBy nvarchar(200) NULL,
        DecidedAt datetime2 NOT NULL,
        TransferStatus nvarchar(50) NOT NULL CONSTRAINT DF_CancelledPieceDisposition_TransferStatus DEFAULT N'Pending',
        ReadyMadeInventoryProductId int NULL,
        TransferredAt datetime2 NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_CancelledPieceDisposition_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_CancelledPieceDisposition PRIMARY KEY (CancelledPieceDispositionId),
        CONSTRAINT FK_CancelledPieceDisposition_Pieces FOREIGN KEY (PieceId) REFERENCES dbo.Pieces(PieceID),
        CONSTRAINT UQ_CancelledPieceDisposition_PieceId UNIQUE (PieceId),
        CONSTRAINT CK_CancelledPieceDisposition_Decision CHECK (Decision IN (N'ContinueToReadyInventory', N'StopAndHold')),
        CONSTRAINT CK_CancelledPieceDisposition_TransferStatus CHECK (TransferStatus IN (N'Pending', N'NotTransferred', N'Transferred', N'Blocked'))
    );

    CREATE INDEX IX_CancelledPieceDisposition_PieceId ON dbo.CancelledPieceDisposition(PieceId);
END;

COMMIT TRANSACTION;
