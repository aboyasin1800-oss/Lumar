using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.DTOs.Payroll;
using LUMAR_ERP_API_V2.Monitoring;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeWriteInfrastructureTests
{
    [Fact]
    public async Task ProofAssignmentCycle_UsesPieceTypeAndStage_SourceOfTruth_OnIsolatedDatabase()
    {
        var connectionString = GetProofConnectionString();
        var repository = CreateRepository(connectionString);
        var employeeCode = CreateUniqueEmployeeCode("PROOF-ASG");
        var otherEmployeeCode = CreateUniqueEmployeeCode("PROOF-OTHER");
        var departmentId = await EnsureActiveDepartmentAsync(connectionString, "ProofDept");

        await SeedOfficialPieceRatesAsync(connectionString, [
            ("كوت", "Cutting"),
            ("قميص", "Cutting"),
            ("قميص", "Sewing")
        ]);

        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف إثبات إسناد معزول",
            DepartmentId = departmentId,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 11, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 10m, null, null, true),
                new EmployeePieceRateAssignmentInputDto("قميص", "Cutting", 12m, null, null, true),
                new EmployeePieceRateAssignmentInputDto("قميص", "Sewing", 14m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var assignments = await repository.GetPieceRateAssignmentsAsync(created!.EmployeeId, CancellationToken.None);
            Assert.Equal(3, assignments.Count);
            Assert.Contains(assignments, item => item.PieceType == "كوت" && item.Stage == "Cutting");
            Assert.Contains(assignments, item => item.PieceType == "قميص" && item.Stage == "Cutting");
            Assert.Contains(assignments, item => item.PieceType == "قميص" && item.Stage == "Sewing");

            var otherEmployee = await repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = otherEmployeeCode,
                FullName = "موظف آخر لإثبات الاستبعاد",
                DepartmentId = departmentId,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 11, 2),
                Status = "Active",
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto("قميص", "Cutting", 12m, null, null, true)
                ]
            }, CancellationToken.None);

            Assert.NotNull(otherEmployee);

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var orderId1 = await InsertOrderAsync(connection, "PROOF-ORD-1");
                var orderItemId1 = await InsertOrderItemAsync(connection, orderId1, "كوت", "TRACK-101");
                var orderItemId2 = await InsertOrderItemAsync(connection, orderId1, "قميص", "TRACK-202");
                var orderItemId3 = await InsertOrderItemAsync(connection, orderId1, "قميص", "TRACK-303");
                var orderId2 = await InsertOrderAsync(connection, "PROOF-ORD-2");
                var orderItemId4 = await InsertOrderItemAsync(connection, orderId2, "قميص", "TRACK-404");

                var pieceId1 = await InsertPieceAsync(connection, orderItemId1, "PROOF-PIECE-101", "Completed", 101);
                var pieceId2 = await InsertPieceAsync(connection, orderItemId2, "PROOF-PIECE-202", "Completed", 202);
                var pieceId3 = await InsertPieceAsync(connection, orderItemId3, "PROOF-PIECE-303", "Completed", 303);
                var pieceId4 = await InsertPieceAsync(connection, orderItemId4, "PROOF-PIECE-404", "Completed", 404);

                var eventForCootCut = await InsertTrackingEventAsync(connection, orderId1, orderItemId1, pieceId1, "PROOF-TRACK-1", "Cutting", "Completed", employeeCode, created!.EmployeeId);
                var eventForShirtCut1 = await InsertTrackingEventAsync(connection, orderId1, orderItemId2, pieceId2, "PROOF-TRACK-2", "Cutting", "Completed", employeeCode, created.EmployeeId);
                var eventForShirtCut2 = await InsertTrackingEventAsync(connection, orderId1, orderItemId2, pieceId2, "PROOF-TRACK-3", "Cutting", "Completed", employeeCode, created.EmployeeId);
                var eventForShirtSew = await InsertTrackingEventAsync(connection, orderId1, orderItemId3, pieceId3, "PROOF-TRACK-4", "Sewing", "Completed", employeeCode, created.EmployeeId);
                var eventForOtherCut = await InsertTrackingEventAsync(connection, orderId2, orderItemId4, pieceId4, "PROOF-TRACK-5", "Cutting", "Completed", otherEmployeeCode, otherEmployee!.EmployeeId);

                await using var insertWages = new SqlCommand(@"
                    INSERT INTO dbo.PieceWageRecords (OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt)
                    VALUES
                        (@order1, @orderItem1, @pieceId1, @event1, @employeeId, @employeeCode, N'كوت', N'Cutting', 1, 10, 10, NULL, NULL, N'PendingPayroll', NULL, SYSDATETIME()),
                        (@order1, @orderItem2, @pieceId2, @event2, @employeeId, @employeeCode, N'قميص', N'Cutting', 1, 12, 12, NULL, NULL, N'PendingPayroll', NULL, SYSDATETIME()),
                        (@order1, @orderItem2, @pieceId2, @event3, @employeeId, @employeeCode, N'قميص', N'Cutting', 1, 12, 12, NULL, NULL, N'PendingPayroll', NULL, SYSDATETIME()),
                        (@order1, @orderItem3, @pieceId3, @event4, @employeeId, @employeeCode, N'قميص', N'Sewing', 1, 14, 14, NULL, NULL, N'PendingPayroll', NULL, SYSDATETIME()),
                        (@order2, @orderItem4, @pieceId4, @event5, @otherEmployeeId, @otherEmployeeCode, N'قميص', N'Cutting', 1, 12, 12, NULL, NULL, N'PendingPayroll', NULL, SYSDATETIME());", connection);

                insertWages.Parameters.AddWithValue("@order1", orderId1);
                insertWages.Parameters.AddWithValue("@order2", orderId2);
                insertWages.Parameters.AddWithValue("@orderItem1", orderItemId1);
                insertWages.Parameters.AddWithValue("@orderItem2", orderItemId2);
                insertWages.Parameters.AddWithValue("@orderItem3", orderItemId3);
                insertWages.Parameters.AddWithValue("@orderItem4", orderItemId4);
                insertWages.Parameters.AddWithValue("@pieceId1", pieceId1);
                insertWages.Parameters.AddWithValue("@pieceId2", pieceId2);
                insertWages.Parameters.AddWithValue("@pieceId3", pieceId3);
                insertWages.Parameters.AddWithValue("@pieceId4", pieceId4);
                insertWages.Parameters.AddWithValue("@employeeId", created.EmployeeId);
                insertWages.Parameters.AddWithValue("@employeeCode", employeeCode);
                insertWages.Parameters.AddWithValue("@otherEmployeeId", otherEmployee.EmployeeId);
                insertWages.Parameters.AddWithValue("@otherEmployeeCode", otherEmployeeCode);
                insertWages.Parameters.AddWithValue("@event1", eventForCootCut);
                insertWages.Parameters.AddWithValue("@event2", eventForShirtCut1);
                insertWages.Parameters.AddWithValue("@event3", eventForShirtCut2);
                insertWages.Parameters.AddWithValue("@event4", eventForShirtSew);
                insertWages.Parameters.AddWithValue("@event5", eventForOtherCut);
                await insertWages.ExecuteNonQueryAsync();

                var cuttingCount = await CountDistinctPieceIdsAsync(connectionString, created.EmployeeId, "كوت", "Cutting");
                var sewingCount = await CountDistinctPieceIdsAsync(connectionString, created.EmployeeId, "قميص", "Sewing");
                var cutSheetCount = await CountDistinctPieceIdsAsync(connectionString, created.EmployeeId, "قميص", "Cutting");
                var otherEmployeeCuttingCount = await CountDistinctPieceIdsAsync(connectionString, otherEmployee.EmployeeId, "قميص", "Cutting");

                Assert.Equal(1, cuttingCount);
                Assert.Equal(1, sewingCount);
                Assert.Equal(1, cutSheetCount);
                Assert.Equal(1, otherEmployeeCuttingCount);
            }

            var updated = await repository.UpdateAsync(created.EmployeeId, new UpdateEmployeeDto
            {
                EmployeeCode = employeeCode,
                FullName = "موظف إثبات إسناد معزول",
                DepartmentId = departmentId,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 11, 1),
                Status = "Active",
                IsActive = true,
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 10m, null, null, true),
                    new EmployeePieceRateAssignmentInputDto("قميص", "Sewing", 14m, null, null, true)
                ]
            }, CancellationToken.None);

            Assert.NotNull(updated);
            var updatedAssignments = await repository.GetPieceRateAssignmentsAsync(created.EmployeeId, CancellationToken.None);
            Assert.Equal(2, updatedAssignments.Count);
            Assert.Contains(updatedAssignments, item => item.PieceType == "كوت" && item.Stage == "Cutting");
            Assert.Contains(updatedAssignments, item => item.PieceType == "قميص" && item.Stage == "Sewing");
            Assert.DoesNotContain(updatedAssignments, item => item.PieceType == "قميص" && item.Stage == "Cutting");

            var reduced = await repository.UpdateAsync(created.EmployeeId, new UpdateEmployeeDto
            {
                EmployeeCode = employeeCode,
                FullName = "موظف إثبات إسناد معزول",
                DepartmentId = departmentId,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 11, 1),
                Status = "Active",
                IsActive = true,
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 10m, null, null, true),
                    new EmployeePieceRateAssignmentInputDto("قميص", "Cutting", 12m, null, null, true)
                ]
            }, CancellationToken.None);

            Assert.NotNull(reduced);
            var reducedAssignments = await repository.GetPieceRateAssignmentsAsync(created.EmployeeId, CancellationToken.None);
            Assert.Equal(2, reducedAssignments.Count);
            Assert.DoesNotContain(reducedAssignments, item => item.PieceType == "قميص" && item.Stage == "Sewing");
        }
        finally
        {
            await DeleteOfficialPieceRatesAsync(connectionString, [
                ("كوت", "Cutting"),
                ("قميص", "Cutting"),
                ("قميص", "Sewing")
            ]);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
            await DeleteEmployeeByCodeAsync(connectionString, otherEmployeeCode);
        }
    }

    [Fact]
    public void CreateEmployee_Valid_Request_Passes_Validation()
    {
        var request = new CreateEmployeeDto
        {
            EmployeeCode = "EMP-001",
            FullName = "علي أحمد",
            DepartmentId = 17,
            Status = "Active",
            BasicSalary = 2500m,
            HireDate = new DateTime(2025, 1, 15)
        };

        var exception = Record.Exception(() => EmployeeWriteValidator.ValidateForCreate(request));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateAsync_BasicSalaryEmployee_KeepsLegacyPieceWageRateZero()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-EMP");

        try
        {
            var repository = CreateRepository(connectionString);
            var created = await repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف راتب أساسي اختبار",
                DepartmentId = 17,
                BasicSalary = 60000m,
                HireDate = new DateTime(2025, 1, 15),
                Status = "Active",
                SalaryType = "BasicSalary"
            }, CancellationToken.None);

            Assert.NotNull(created);
            Assert.Equal(60000m, created!.BasicSalary);
            Assert.Equal(0m, created.PieceWageRate);
            Assert.Equal(60000m, await ReadDecimalAsync(connectionString, "SELECT BasicSalary FROM dbo.Employees WHERE EmployeeCode = @code", code));
            Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT PieceWageRate FROM dbo.Employees WHERE EmployeeCode = @code", code));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task CreateAsync_PieceWageEmployee_PersistsZeroLegacyRateAndIndependentAssignments()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-PIECE");

        try
        {
            var repository = CreateRepository(connectionString);
            var created = await repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف قطعة اختبار",
                DepartmentId = 17,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 2, 10),
                Status = "Active",
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto("بنطلون", "Cutting", 22.50m, null, null, true)
                ]
            }, CancellationToken.None);

            Assert.NotNull(created);
            Assert.Equal(0m, created!.BasicSalary);
            Assert.Equal(0m, created.PieceWageRate);
            Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT PieceWageRate FROM dbo.Employees WHERE EmployeeCode = @code", code));
            Assert.Equal(1, await CountAssignmentsAsync(connectionString, created.EmployeeId));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task CreateAsync_PieceWageWithInvalidPieceRate_RollsBackEntireEmployee()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-ROLL");

        try
        {
            var repository = CreateRepository(connectionString);
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف قطعة فاشل",
                DepartmentId = 17,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 3, 14),
                Status = "Active",
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto(string.Empty, "Cutting", 22.50m, null, null, true)
                ]
            }, CancellationToken.None));

            Assert.Equal(0, await CountEmployeesAsync(connectionString, code));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public void CreateEmployee_Rejects_Missing_Data()
    {
        var request = new CreateEmployeeDto
        {
            EmployeeCode = "   ",
            FullName = "",
            Status = "",
            BasicSalary = 0m
        };

        var exception = Assert.Throws<ArgumentException>(() => EmployeeWriteValidator.ValidateForCreate(request));

        Assert.Contains("FullName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_Code_Validation_Rejects_Existing_Code()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EmployeeWriteValidator.EnsureUniqueCode("EMP-001", new[] { "EMP-001" }, null));

        Assert.Contains("EmployeeCode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateEmployee_Valid_Request_Passes_Validation()
    {
        var request = new UpdateEmployeeDto
        {
            EmployeeCode = "EMP-001",
            FullName = "علي أحمد",
            DepartmentId = 17,
            Status = "Active",
            BasicSalary = 2800m,
            HireDate = new DateTime(2025, 1, 15),
            IsActive = true
        };

        var exception = Record.Exception(() => EmployeeWriteValidator.ValidateForUpdate(request));

        Assert.Null(exception);
    }

    [Fact]
    public void ActivateEmployee_Normalizes_Status_To_Active()
    {
        Assert.Equal("Active", EmployeeWriteValidator.NormalizeStatus("active"));
    }

    [Fact]
    public void DeactivateEmployee_Normalizes_Status_To_Inactive()
    {
        Assert.Equal("Inactive", EmployeeWriteValidator.NormalizeStatus("inactive"));
    }

    [Fact]
    public async Task GetWorkflowAsync_UsesOfficialPieceRateAssignments_AndIgnoresLegacyWorkflowTable()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-WORK");

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف سير عمل اختبار",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 6, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var workflow = await repository.GetWorkflowAsync(code, CancellationToken.None);

            Assert.NotEmpty(workflow);
            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) && string.Equals(item.WorkStage, "Cutting", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task GetWorkflowAsync_IncludesLinkedScannerPermissionStages_ForGeneralStageExecution()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-GEN");
        var scannerCode = $"SCN-{Guid.NewGuid():N}"[..20];

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف تجهيز عام اختبار",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 6, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            await using (var scannerCommand = new SqlCommand(@"INSERT INTO dbo.Scanners (ScannerCode, ScannerName, Description, IsActive, CreatedAt, UpdatedAt)
VALUES (@scannerCode, @scannerName, @description, 1, SYSDATETIME(), SYSDATETIME());", connection))
            {
                scannerCommand.Parameters.AddWithValue("@scannerCode", scannerCode);
                scannerCommand.Parameters.AddWithValue("@scannerName", $"Scanner {scannerCode}");
                scannerCommand.Parameters.AddWithValue("@description", "General stage workflow regression");
                await scannerCommand.ExecuteNonQueryAsync();
            }

            var scannerId = await ReadIntAsync(connectionString, "SELECT ScannerId FROM dbo.Scanners WHERE ScannerCode = @code", scannerCode);
            await using (var permissionCommand = new SqlCommand(@"INSERT INTO dbo.ScannerStageProductPermissions (ScannerId, ProductTypeId, Stage, IsActive, CreatedAt, UpdatedAt)
VALUES (@scannerId, 1, 'Assembly', 1, SYSDATETIME(), SYSDATETIME());", connection))
            {
                permissionCommand.Parameters.AddWithValue("@scannerId", scannerId);
                await permissionCommand.ExecuteNonQueryAsync();
            }

            await using (var linkCommand = new SqlCommand(@"UPDATE dbo.Employees SET ScannerCode = @scannerCode, UpdatedAt = SYSDATETIME() WHERE EmployeeId = @employeeId;", connection))
            {
                linkCommand.Parameters.AddWithValue("@scannerCode", scannerCode);
                linkCommand.Parameters.AddWithValue("@employeeId", created!.EmployeeId);
                await linkCommand.ExecuteNonQueryAsync();
            }
        }

        try
        {
            var workflow = await repository.GetWorkflowAsync(code, CancellationToken.None);

            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) &&
                string.Equals(item.WorkStage, "Assembly", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) &&
                string.Equals(item.WorkStage, "Cutting", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using (var cleanup = new SqlCommand(@"DELETE FROM dbo.ScannerStageProductPermissions WHERE ScannerId IN (SELECT ScannerId FROM dbo.Scanners WHERE ScannerCode = @scannerCode);
DELETE FROM dbo.Scanners WHERE ScannerCode = @scannerCode;
UPDATE dbo.Employees SET ScannerCode = NULL, UpdatedAt = SYSDATETIME() WHERE EmployeeCode = @employeeCode;", connection))
                {
                    cleanup.Parameters.AddWithValue("@scannerCode", scannerCode);
                    cleanup.Parameters.AddWithValue("@employeeCode", code);
                    await cleanup.ExecuteNonQueryAsync();
                }
            }

            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task UpdateAsync_BasicSalaryEmployee_ClearsStalePieceAssignments()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-STALE");

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var seed = new SqlCommand(@"INSERT INTO dbo.PieceWageRates (PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt)
VALUES (N'كوت', N'Cutting', 120.00, 1, N'Test rate', SYSDATETIME(), SYSDATETIME()),
       (N'بنطلون', N'Sewing', 140.00, 1, N'Test rate', SYSDATETIME(), SYSDATETIME());", connection);
            await seed.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف تحديث مرتبطة بالقطعة",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 7, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true),
                new EmployeePieceRateAssignmentInputDto("بنطلون", "Sewing", 35.00m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var updated = await repository.UpdateAsync(created!.EmployeeId, new UpdateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف تحديث راتب أساسي",
                DepartmentId = 17,
                BasicSalary = 4200m,
                HireDate = new DateTime(2025, 7, 1),
                Status = "Active",
                IsActive = true,
                SalaryType = "BasicSalary",
                PieceRates = null
            }, CancellationToken.None);

            Assert.NotNull(updated);
            Assert.Equal(4200m, updated!.BasicSalary);
            Assert.Equal("BasicSalary", updated.SalaryType);
            Assert.Equal(0, await CountAssignmentsAsync(connectionString, created.EmployeeId));
            Assert.Empty(await repository.GetWorkflowAsync(code, CancellationToken.None));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task SafeRequestCaptureMiddleware_WhenLogFileIsLocked_DoesNotThrow() 
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LUMAR_ERP_API_V2",
            "safe-live-monitor.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(logPath, string.Empty);

        await using var lockedHandle = new FileStream(logPath, FileMode.Open, FileAccess.Write, FileShare.None);
        var middleware = new SafeRequestCaptureMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/production/scan";
        context.Response.StatusCode = StatusCodes.Status200OK;

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAllAsync_And_GetDepartmentsAsync_DoNotThrow_WhenParametersAreNull()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);

        var employees = await repository.GetAllAsync(CancellationToken.None);
        var departments = await repository.GetDepartmentsAsync(CancellationToken.None);

        Assert.NotNull(employees);
        Assert.NotNull(departments);
        Assert.NotEmpty(employees);
        Assert.NotEmpty(departments);
    }

    [Fact]
    public async Task RemainingWages_PayrollTransition_CountsPendingPieceWageOnce()
    {
        var connectionString = GetProofConnectionString();
        var employeeRepository = CreateRepository(connectionString);
        var payrollRepository = new PayrollRepository(
            new ReadOnlySqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })));
        var departmentId = await EnsureActiveDepartmentAsync(connectionString, "Order85RemainingWages");
        var employeeCode = CreateUniqueEmployeeCode("O85-RW");
        await SeedOfficialPieceRatesAsync(connectionString, [("كوت", "Cutting")]);

        var created = await employeeRepository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف متبقي أجور الأمر 85",
            DepartmentId = departmentId,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 1, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 2000m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var proofTrackingCode = $"O85-{Guid.NewGuid():N}"[..20];
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var orderId = await InsertOrderAsync(connection, $"O85-{Guid.NewGuid():N}"[..20]);
                var orderItemId = await InsertOrderItemAsync(connection, orderId, "كوت", proofTrackingCode);
                var pieceId = await InsertPieceAsync(connection, orderItemId, proofTrackingCode, "Completed", 85);
                var trackingEventId = await InsertTrackingEventAsync(connection, orderId, orderItemId, pieceId, proofTrackingCode, "Cutting", "Completed", employeeCode, created!.EmployeeId);

                await using var insertWage = new SqlCommand(@"
                    INSERT INTO dbo.PieceWageRecords
                        (OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt)
                    VALUES
                        (@orderId, @orderItemId, @pieceId, @trackingEventId, @employeeId, @employeeCode, N'كوت', N'Cutting', 1, 2000, 2000, NULL, NULL, N'PendingPayroll', N'Order85 isolated proof', SYSDATETIME());", connection);
                insertWage.Parameters.AddWithValue("@orderId", orderId);
                insertWage.Parameters.AddWithValue("@orderItemId", orderItemId);
                insertWage.Parameters.AddWithValue("@pieceId", pieceId);
                insertWage.Parameters.AddWithValue("@trackingEventId", trackingEventId);
                insertWage.Parameters.AddWithValue("@employeeId", created.EmployeeId);
                insertWage.Parameters.AddWithValue("@employeeCode", employeeCode);
                await insertWage.ExecuteNonQueryAsync();
            }

            var before = await employeeRepository.GetSelfFinancialSummaryAsync(created.EmployeeId, null, CancellationToken.None);
            Assert.NotNull(before);
            Assert.Equal(2000m, before!.PieceWageAmount);
            Assert.Equal(2000m, before.RemainingWages);

            var now = DateTime.UtcNow.Date;
            var result = await payrollRepository.GenerateAsync(new GeneratePayrollRequestDto(
                now,
                now,
                $"O85-{Guid.NewGuid():N}",
                "Isolated remaining wages proof"), CancellationToken.None);

            Assert.Contains(created.EmployeeId, await GetPayrollEmployeeIdsAsync(connectionString, result.PayrollPeriodId));

            var after = await employeeRepository.GetSelfFinancialSummaryAsync(created.EmployeeId, null, CancellationToken.None);
            Assert.NotNull(after);
            Assert.Equal(2000m, after!.PieceWageAmount);

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var count = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM dbo.PieceWageRecords
                    WHERE EmployeeId = @employeeId
                      AND TotalWage = 2000
                      AND PayrollRecordId IS NOT NULL
                      AND Status = N'Generated';", connection);
                count.Parameters.AddWithValue("@employeeId", created.EmployeeId);
                Assert.Equal(1, Convert.ToInt32(await count.ExecuteScalarAsync()));
            }

            Assert.Equal(0m, after.RemainingWages);
        }
        finally
        {
            await DeletePieceWageAndPayrollForEmployeeAsync(connectionString, created.EmployeeId);
            await DeleteProofTrackingFixtureAsync(connectionString, employeeCode);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
            await DeleteOfficialPieceRatesAsync(connectionString, [("كوت", "Cutting")]);
            await DeleteDepartmentByCodeAsync(connectionString, "Order85RemainingWages");
        }
    }

    [Fact]
    public async Task GetSelfFinancialSummaryAsync_WithPeriodId_ReturnsSummary_WithoutArgumentException()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);
        var employeeCode = CreateUniqueEmployeeCode("ES7-FIN");

        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف مالي اختبار",
            DepartmentId = 17,
            BasicSalary = 4200m,
            HireDate = new DateTime(2025, 4, 1),
            Status = "Active",
            SalaryType = "BasicSalary"
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var rowValue = await EnsurePayrollRecordExistsAsync(connectionString, created!.EmployeeId);

            var exception = await Record.ExceptionAsync(() => repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, rowValue.PayrollPeriodId, CancellationToken.None));

            Assert.Null(exception);
            var summary = await repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, rowValue.PayrollPeriodId, CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(rowValue.EmployeeId, summary!.EmployeeId);
            Assert.Equal(rowValue.PayrollPeriodId, summary.PayrollPeriodId);
        }
        finally
        {
            await DeletePayrollRecordAsync(connectionString, created!.EmployeeId);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
        }
    }

    [Fact]
    public async Task GetSelfFinancialSummaryAsync_WithoutPeriodId_UsesAuthenticatedEmployeeId_WithoutArgumentException()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);
        var employeeCode = CreateUniqueEmployeeCode("ES7-FIN");

        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف مالي اختبار بدون فترة",
            DepartmentId = 17,
            BasicSalary = 5000m,
            HireDate = new DateTime(2025, 4, 2),
            Status = "Active",
            SalaryType = "BasicSalary"
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var rowValue = await EnsurePayrollRecordExistsAsync(connectionString, created!.EmployeeId);

            var exception = await Record.ExceptionAsync(() => repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, null, CancellationToken.None));

            Assert.Null(exception);
            var summary = await repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, null, CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(rowValue.EmployeeId, summary!.EmployeeId);
        }
        finally
        {
            await DeletePayrollRecordAsync(connectionString, created!.EmployeeId);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
        }
    }

    private static EmployeeRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new EmployeeRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");

        var builder = new SqlConnectionStringBuilder(value);
        var allowed = new[]
        {
            "LUMAR_ERP_ES_VALIDATION",
            "LUMAR_ERP_ES_VALIDATION_R3",
            "LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF"
        };

        if (!allowed.Contains(builder.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Employee integration tests require an approved validation database.");

        return builder.ConnectionString;
    }

    private static string GetProofConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        var builder = new SqlConnectionStringBuilder(value)
        {
            DataSource = "YASIN-YASIN\\SQLEXPRESS",
            InitialCatalog = "LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };

        return builder.ConnectionString;
    }

    private static string CreateUniqueEmployeeCode(string prefix, int maxLength = 20)
    {
        var candidate = $"{prefix}-{Guid.NewGuid():N}";
        return candidate.Length <= maxLength ? candidate : candidate[..maxLength];
    }

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? 0m : Convert.ToDecimal(value);
    }

    private static async Task<int> ReadIntAsync(string connectionString, string sql, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? 0 : Convert.ToInt32(value);
    }

    private static async Task<int> CountAssignmentsAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId", connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<(int EmployeeId, int PayrollPeriodId)> EnsurePayrollRecordExistsAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var existing = new SqlCommand(@"SELECT TOP (1) EmployeeId, PayrollPeriodId FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId ORDER BY PayrollPeriodId DESC, CreatedAt DESC", connection))
        {
            existing.Parameters.AddWithValue("@employeeId", employeeId);
            await using var reader = await existing.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetInt32(1));
            }
        }

        await using (var period = new SqlCommand(@"INSERT INTO dbo.PayrollPeriods (PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, CreatedAt) OUTPUT INSERTED.PayrollPeriodId VALUES (@periodCode, @startDate, @endDate, @status, @notes, @generatedAt, @createdAt)", connection))
        {
            var startDate = DateTime.UtcNow.AddMonths(-1).Date;
            var endDate = startDate.AddMonths(1).AddDays(-1);
            period.Parameters.AddWithValue("@periodCode", $"TEST-{Guid.NewGuid():N}");
            period.Parameters.AddWithValue("@startDate", startDate);
            period.Parameters.AddWithValue("@endDate", endDate);
            period.Parameters.AddWithValue("@status", "Generated");
            period.Parameters.AddWithValue("@notes", (object?)DBNull.Value);
            period.Parameters.AddWithValue("@generatedAt", DateTime.UtcNow);
            period.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            var periodId = await period.ExecuteScalarAsync();
            if (periodId is not int payrollPeriodId)
                throw new InvalidOperationException("Payroll period could not be created for the financial summary test.");

            await using (var record = new SqlCommand(@"INSERT INTO dbo.PayrollRecords (PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt) OUTPUT INSERTED.PayrollPeriodId VALUES (@periodId, @employeeId, @basicSalaryAmount, @pieceWageAmount, @attendanceAdjustmentAmount, @overtimeAmount, @grossAmount, @deductionsAmount, @netAmount, @status, @notes, @createdAt)", connection))
            {
                record.Parameters.AddWithValue("@periodId", payrollPeriodId);
                record.Parameters.AddWithValue("@employeeId", employeeId);
                record.Parameters.AddWithValue("@basicSalaryAmount", 4200m);
                record.Parameters.AddWithValue("@pieceWageAmount", 0m);
                record.Parameters.AddWithValue("@attendanceAdjustmentAmount", 0m);
                record.Parameters.AddWithValue("@overtimeAmount", 0m);
                record.Parameters.AddWithValue("@grossAmount", 4200m);
                record.Parameters.AddWithValue("@deductionsAmount", 0m);
                record.Parameters.AddWithValue("@netAmount", 4200m);
                record.Parameters.AddWithValue("@status", "Approved");
                record.Parameters.AddWithValue("@notes", (object?)DBNull.Value);
                record.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

                var insertedPeriodId = await record.ExecuteScalarAsync();
                if (insertedPeriodId is not int insertedPayrollPeriodId)
                    throw new InvalidOperationException("Payroll record could not be created for the financial summary test.");

                return (employeeId, insertedPayrollPeriodId);
            }
        }
    }

    private static async Task<IReadOnlyList<int>> GetPayrollEmployeeIdsAsync(string connectionString, int periodId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT EmployeeId FROM dbo.PayrollRecords WHERE PayrollPeriodId = @periodId", connection);
        command.Parameters.AddWithValue("@periodId", periodId);
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<int>();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        return ids;
    }

    private static async Task DeletePieceWageAndPayrollForEmployeeAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var wage = new SqlCommand("DELETE FROM dbo.PieceWageRecords WHERE EmployeeId = @employeeId", connection);
        wage.Parameters.AddWithValue("@employeeId", employeeId);
        await wage.ExecuteNonQueryAsync();

        await using var periodIds = new SqlCommand("SELECT DISTINCT PayrollPeriodId FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection);
        periodIds.Parameters.AddWithValue("@employeeId", employeeId);
        await using var reader = await periodIds.ExecuteReaderAsync();
        var ids = new List<int>();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        await reader.DisposeAsync();

        await using var records = new SqlCommand("DELETE FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection);
        records.Parameters.AddWithValue("@employeeId", employeeId);
        await records.ExecuteNonQueryAsync();

        foreach (var periodId in ids)
        {
            await using var period = new SqlCommand("DELETE FROM dbo.PayrollPeriods WHERE PayrollPeriodId = @periodId", connection);
            period.Parameters.AddWithValue("@periodId", periodId);
            await period.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteProofTrackingFixtureAsync(string connectionString, string employeeCode)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var deleteEvents = new SqlCommand("DELETE FROM dbo.TrackingEvents WHERE EmployeeCode = @employeeCode", connection);
        deleteEvents.Parameters.AddWithValue("@employeeCode", employeeCode);
        await deleteEvents.ExecuteNonQueryAsync();
        await using var deletePieces = new SqlCommand(@"
            DELETE p FROM dbo.Pieces p
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            WHERE o.OrderNumber LIKE N'O85-%';", connection);
        await deletePieces.ExecuteNonQueryAsync();
        await using var deleteItems = new SqlCommand("DELETE oi FROM dbo.OrderItems oi INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID WHERE o.OrderNumber LIKE N'O85-%';", connection);
        await deleteItems.ExecuteNonQueryAsync();
        await using var deleteOrders = new SqlCommand("DELETE FROM dbo.Orders WHERE OrderNumber LIKE N'O85-%';", connection);
        await deleteOrders.ExecuteNonQueryAsync();
    }

    private static async Task DeleteDepartmentByCodeAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM dbo.Departments WHERE DepartmentCode = @code", connection);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeletePayrollRecordAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var selectPeriodIds = new SqlCommand("SELECT DISTINCT PayrollPeriodId FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection))
        {
            selectPeriodIds.Parameters.AddWithValue("@employeeId", employeeId);
            await using var reader = await selectPeriodIds.ExecuteReaderAsync();
            var periodIds = new List<int>();
            while (await reader.ReadAsync())
            {
                periodIds.Add(reader.GetInt32(0));
            }

            if (periodIds.Count > 0)
            {
                await using (var deleteRecords = new SqlCommand("DELETE FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection))
                {
                    deleteRecords.Parameters.AddWithValue("@employeeId", employeeId);
                    await deleteRecords.ExecuteNonQueryAsync();
                }

                foreach (var periodId in periodIds)
                {
                    await using var deletePeriod = new SqlCommand("DELETE FROM dbo.PayrollPeriods WHERE PayrollPeriodId = @periodId", connection);
                    deletePeriod.Parameters.AddWithValue("@periodId", periodId);
                    await deletePeriod.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private static async Task SeedOfficialPieceRatesAsync(string connectionString, IReadOnlyList<(string PieceType, string Stage)> rates)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var (pieceType, stage) in rates)
        {
            await using var command = new SqlCommand(@"
                INSERT INTO dbo.PieceWageRates (PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt)
                SELECT @pieceType, @stage, 10.00, 1, N'Test proof rate', SYSDATETIME(), SYSDATETIME()
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.PieceWageRates WITH (NOLOCK)
                    WHERE PieceType = @pieceType AND Stage = @stage AND IsActive = 1
                );", connection);
            command.Parameters.AddWithValue("@pieceType", pieceType);
            command.Parameters.AddWithValue("@stage", stage);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteOfficialPieceRatesAsync(string connectionString, IReadOnlyList<(string PieceType, string Stage)> rates)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var (pieceType, stage) in rates)
        {
            await using var command = new SqlCommand(
                "DELETE FROM dbo.PieceWageRates WHERE PieceType = @pieceType AND Stage = @stage AND Notes = N'Test proof rate'",
                connection);
            command.Parameters.AddWithValue("@pieceType", pieceType);
            command.Parameters.AddWithValue("@stage", stage);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> EnsureActiveDepartmentAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var existing = new SqlCommand("SELECT TOP (1) DepartmentId FROM dbo.Departments WHERE DepartmentCode = @code AND IsActive = 1", connection))
        {
            existing.Parameters.AddWithValue("@code", code);
            var existingId = await existing.ExecuteScalarAsync();
            if (existingId is int departmentId)
                return departmentId;
        }

        await using var insert = new SqlCommand(@"INSERT INTO dbo.Departments (DepartmentCode, DepartmentName, Description, IsActive, CreatedAt, UpdatedAt)
OUTPUT INSERTED.DepartmentId
VALUES (@code, @name, @description, 1, SYSDATETIME(), SYSDATETIME());", connection);
        insert.Parameters.AddWithValue("@code", code);
        insert.Parameters.AddWithValue("@name", "Proof Department");
        insert.Parameters.AddWithValue("@description", "Temporary department for isolated proof test.");
        var insertedId = await insert.ExecuteScalarAsync();
        return insertedId is int id ? id : throw new InvalidOperationException("Unable to create proof department.");
    }

    private static async Task<int> InsertOrderAsync(SqlConnection connection, string orderNumber)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.Orders (OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, CreatedDate, SaleCategory, RevenueRecognized, RevenueReversalCreated)
            OUTPUT INSERTED.OrderID
            VALUES (@orderNumber, 1, SYSDATETIME(), DATEADD(day, 7, SYSDATETIME()), 1000, 0, 0, 1000, N'Normal', N'New', SYSDATETIME(), N'TailoringOrder', 0, 0);", connection);
        command.Parameters.AddWithValue("@orderNumber", orderNumber);
        var insertedId = await command.ExecuteScalarAsync();
        return insertedId is int id ? id : throw new InvalidOperationException("Unable to insert proof order.");
    }

    private static async Task<int> InsertOrderItemAsync(SqlConnection connection, int orderId, string pieceType, string trackingCode)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.OrderItems (OrderID, PieceType, Quantity, MeasurementSnapshot, TrackingCode, PieceStatus, CreatedDate, ProductTypeId)
            OUTPUT INSERTED.OrderItemID
            VALUES (@orderId, @pieceType, 1, N'{}', @trackingCode, N'New', SYSDATETIME(), 1);", connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@pieceType", pieceType);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        var insertedId = await command.ExecuteScalarAsync();
        return insertedId is int id ? id : throw new InvalidOperationException("Unable to insert proof order item.");
    }

    private static async Task<int> InsertPieceAsync(SqlConnection connection, int orderItemId, string trackingCode, string pieceStatus, int pieceNumber)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.Pieces (OrderItemID, TrackingCode, PieceStatus, PieceNumber, CreatedDate)
            OUTPUT INSERTED.PieceID
            VALUES (@orderItemId, @trackingCode, @pieceStatus, @pieceNumber, SYSDATETIME());", connection);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        command.Parameters.AddWithValue("@pieceStatus", pieceStatus);
        command.Parameters.AddWithValue("@pieceNumber", pieceNumber);
        var insertedId = await command.ExecuteScalarAsync();
        return insertedId is int id ? id : throw new InvalidOperationException("Unable to insert proof piece.");
    }

    private static async Task<int> InsertTrackingEventAsync(SqlConnection connection, int orderId, int orderItemId, int pieceId, string trackingCode, string stage, string status, string employeeCode, int employeeId)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.TrackingEvents (OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, EmployeeCode, IsReverted, PieceID)
            OUTPUT INSERTED.TrackingEventID
            VALUES (@orderItemId, @orderId, @trackingCode, @stage, @status, SYSDATETIME(), @employeeCode, 0, @pieceId);", connection);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        command.Parameters.AddWithValue("@stage", stage);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@employeeCode", employeeCode);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var insertedId = await command.ExecuteScalarAsync();
        return insertedId is int id ? id : throw new InvalidOperationException("Unable to insert proof tracking event.");
    }

    private static async Task<int> CountEmployeesAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Employees WHERE EmployeeCode = @code", connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountDistinctPieceIdsAsync(string connectionString, int employeeId, string pieceType, string stage)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(DISTINCT PieceId) FROM dbo.PieceWageRecords WHERE EmployeeId = @employeeId AND PieceType = @pieceType AND Stage = @stage AND Status <> N'Cancelled' AND Status <> N'Canceled' AND Status <> N'Rejected' AND Status <> N'Void' AND Status <> N'Voided';",
            connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@pieceType", pieceType);
        command.Parameters.AddWithValue("@stage", stage);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? 0 : Convert.ToInt32(result);
    }

    private static async Task DeleteEmployeeByCodeAsync(string connectionString, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var find = new SqlCommand("SELECT EmployeeId FROM dbo.Employees WHERE EmployeeCode = @code", connection))
        {
            find.Parameters.AddWithValue("@code", code);
            var employeeId = await find.ExecuteScalarAsync();
            if (employeeId is int id)
            {
                await using (var deleteContracts = new SqlCommand("DELETE FROM dbo.EmployeeContracts WHERE EmployeeId = @employeeId", connection))
                {
                    deleteContracts.Parameters.AddWithValue("@employeeId", id);
                    await deleteContracts.ExecuteNonQueryAsync();
                }

                await using (var deleteAssignments = new SqlCommand("DELETE FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId", connection))
                {
                    deleteAssignments.Parameters.AddWithValue("@employeeId", id);
                    await deleteAssignments.ExecuteNonQueryAsync();
                }

                await using (var deleteEmployee = new SqlCommand("DELETE FROM dbo.Employees WHERE EmployeeId = @employeeId", connection))
                {
                    deleteEmployee.Parameters.AddWithValue("@employeeId", id);
                    await deleteEmployee.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
