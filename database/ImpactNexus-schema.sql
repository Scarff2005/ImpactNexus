IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Countries] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [IsoCode] nvarchar(3) NULL,
        CONSTRAINT [PK_Countries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [RegistrationNumber] nvarchar(60) NULL,
        [Industry] nvarchar(80) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Website] nvarchar(200) NULL,
        [Email] nvarchar(150) NULL,
        [Phone] nvarchar(30) NULL,
        [HeadOffice] nvarchar(250) NULL,
        [CountryId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Companies_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Provinces] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [CountryId] int NOT NULL,
        CONSTRAINT [PK_Provinces] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Provinces_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [CompanyId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [BursaryProgrammes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [CompanyId] int NOT NULL,
        [AcademicYear] int NOT NULL,
        [Budget] decimal(18,2) NOT NULL,
        [OpenDate] date NOT NULL,
        [CloseDate] date NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_BursaryProgrammes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BursaryProgrammes_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Towns] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ProvinceId] int NOT NULL,
        CONSTRAINT [PK_Towns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Towns_Provinces_ProvinceId] FOREIGN KEY ([ProvinceId]) REFERENCES [Provinces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Stakeholders] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(150) NULL,
        [Phone] nvarchar(30) NULL,
        [Type] int NOT NULL,
        [CompanyId] int NULL,
        [UserId] nvarchar(450) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Stakeholders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Stakeholders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Stakeholders_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [AddressLine] nvarchar(250) NULL,
        [TownId] int NOT NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Locations_Towns_TownId] FOREIGN KEY ([TownId]) REFERENCES [Towns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Beneficiaries] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(150) NULL,
        [Phone] nvarchar(30) NULL,
        [TownId] int NULL,
        [CompanyId] int NULL,
        [UserId] nvarchar(450) NULL,
        [StakeholderId] int NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Beneficiaries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Beneficiaries_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Beneficiaries_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Beneficiaries_Stakeholders_StakeholderId] FOREIGN KEY ([StakeholderId]) REFERENCES [Stakeholders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Beneficiaries_Towns_TownId] FOREIGN KEY ([TownId]) REFERENCES [Towns] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [CsrActivities] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Objectives] nvarchar(2000) NULL,
        [CompanyId] int NOT NULL,
        [LocationId] int NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [Budget] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [ImpactIndicator] nvarchar(100) NULL,
        [ManagerId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CsrActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CsrActivities_AspNetUsers_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CsrActivities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CsrActivities_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [VolunteerProgrammes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [CompanyId] int NOT NULL,
        [LocationId] int NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_VolunteerProgrammes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VolunteerProgrammes_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VolunteerProgrammes_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [BursaryApplications] (
        [Id] int NOT NULL IDENTITY,
        [BursaryProgrammeId] int NOT NULL,
        [BeneficiaryId] int NOT NULL,
        [Status] int NOT NULL,
        [AmountAwarded] decimal(18,2) NULL,
        [AppliedOn] date NOT NULL,
        [DecisionOn] date NULL,
        [Motivation] nvarchar(2000) NOT NULL,
        [ReviewNotes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BursaryApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BursaryApplications_Beneficiaries_BeneficiaryId] FOREIGN KEY ([BeneficiaryId]) REFERENCES [Beneficiaries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BursaryApplications_BursaryProgrammes_BursaryProgrammeId] FOREIGN KEY ([BursaryProgrammeId]) REFERENCES [BursaryProgrammes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [Donations] (
        [Id] int NOT NULL IDENTITY,
        [DonorName] nvarchar(150) NOT NULL,
        [CompanyId] int NOT NULL,
        [DonorUserId] nvarchar(450) NULL,
        [CsrActivityId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [DonatedOn] date NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Status] int NOT NULL,
        [Reference] nvarchar(60) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Donations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Donations_AspNetUsers_DonorUserId] FOREIGN KEY ([DonorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Donations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Donations_CsrActivities_CsrActivityId] FOREIGN KEY ([CsrActivityId]) REFERENCES [CsrActivities] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE TABLE [VolunteerHours] (
        [Id] int NOT NULL IDENTITY,
        [VolunteerProgrammeId] int NOT NULL,
        [CsrActivityId] int NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Hours] decimal(5,2) NOT NULL,
        [Date] date NOT NULL,
        [Notes] nvarchar(500) NULL,
        [Approved] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_VolunteerHours] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VolunteerHours_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_VolunteerHours_CsrActivities_CsrActivityId] FOREIGN KEY ([CsrActivityId]) REFERENCES [CsrActivities] ([Id]),
        CONSTRAINT [FK_VolunteerHours_VolunteerProgrammes_VolunteerProgrammeId] FOREIGN KEY ([VolunteerProgrammeId]) REFERENCES [VolunteerProgrammes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_CompanyId] ON [AspNetUsers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Beneficiaries_CompanyId] ON [Beneficiaries] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Beneficiaries_StakeholderId] ON [Beneficiaries] ([StakeholderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Beneficiaries_TownId] ON [Beneficiaries] ([TownId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Beneficiaries_UserId] ON [Beneficiaries] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BursaryApplications_BeneficiaryId] ON [BursaryApplications] ([BeneficiaryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BursaryApplications_BursaryProgrammeId] ON [BursaryApplications] ([BursaryProgrammeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BursaryProgrammes_CompanyId] ON [BursaryProgrammes] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_CountryId] ON [Companies] ([CountryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_Name] ON [Companies] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Countries_Name] ON [Countries] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CsrActivities_CompanyId] ON [CsrActivities] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CsrActivities_LocationId] ON [CsrActivities] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CsrActivities_ManagerId] ON [CsrActivities] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Donations_CompanyId] ON [Donations] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Donations_CsrActivityId] ON [Donations] ([CsrActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Donations_DonorUserId] ON [Donations] ([DonorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Locations_TownId_Name] ON [Locations] ([TownId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Provinces_CountryId_Name] ON [Provinces] ([CountryId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Stakeholders_CompanyId] ON [Stakeholders] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Stakeholders_UserId] ON [Stakeholders] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Towns_ProvinceId_Name] ON [Towns] ([ProvinceId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VolunteerHours_CsrActivityId] ON [VolunteerHours] ([CsrActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VolunteerHours_UserId] ON [VolunteerHours] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VolunteerHours_VolunteerProgrammeId] ON [VolunteerHours] ([VolunteerProgrammeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VolunteerProgrammes_CompanyId] ON [VolunteerProgrammes] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VolunteerProgrammes_LocationId] ON [VolunteerProgrammes] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019235341_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019235341_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

