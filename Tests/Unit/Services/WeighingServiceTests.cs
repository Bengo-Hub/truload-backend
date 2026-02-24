using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Implementations.Weighing;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.DTOs.Weighing;
using Xunit;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Yard;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Data;
using TruLoad.Backend.Data.Repositories.Infrastructure;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Services.Interfaces.System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Truload.Backend.Tests.Unit.Services;

public class WeighingServiceTests
{
    private readonly Mock<IWeighingRepository> _mockWeighingRepository;
    private readonly Mock<IAxleConfigurationRepository> _mockAxleConfigurationRepository;
    private readonly Mock<IPermitRepository> _mockPermitRepository;
    private readonly Mock<IProhibitionRepository> _mockProhibitionRepository;
    private readonly Mock<IToleranceRepository> _mockToleranceRepository;
    private readonly Mock<IAxleFeeScheduleRepository> _mockFeeScheduleRepository;
    private readonly Mock<IPdfService> _mockPdfService;
    private readonly Mock<IBlobStorageService> _mockBlobStorageService;
    private readonly Mock<IDocumentRepository> _mockDocumentRepository;
    private readonly Mock<IAxleGroupAggregationService> _mockAxleGroupAggregationService;
    private readonly Mock<IScaleTestRepository> _mockScaleTestRepository;
    private readonly Mock<IVehicleRepository> _mockVehicleRepository;
    private readonly Mock<ICaseRegisterService> _mockCaseRegisterService;
    private readonly Mock<IYardService> _mockYardService;
    private readonly Mock<IVehicleTagService> _mockVehicleTagService;
    private readonly TruLoadDbContext _dbContext;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IDocumentNumberService> _mockDocumentNumberService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<WeighingService>> _mockLogger;
    private readonly WeighingService _service;

    public WeighingServiceTests()
    {
        _mockWeighingRepository = new Mock<IWeighingRepository>();
        _mockAxleConfigurationRepository = new Mock<IAxleConfigurationRepository>();
        _mockPermitRepository = new Mock<IPermitRepository>();
        _mockProhibitionRepository = new Mock<IProhibitionRepository>();
        _mockToleranceRepository = new Mock<IToleranceRepository>();
        _mockFeeScheduleRepository = new Mock<IAxleFeeScheduleRepository>();
        _mockPdfService = new Mock<IPdfService>();
        _mockBlobStorageService = new Mock<IBlobStorageService>();
        _mockDocumentRepository = new Mock<IDocumentRepository>();
        _mockAxleGroupAggregationService = new Mock<IAxleGroupAggregationService>();
        _mockScaleTestRepository = new Mock<IScaleTestRepository>();
        _mockVehicleRepository = new Mock<IVehicleRepository>();
        _mockCaseRegisterService = new Mock<ICaseRegisterService>();
        _mockYardService = new Mock<IYardService>();
        _mockVehicleTagService = new Mock<IVehicleTagService>();
        var dbOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: $"WeighingTest_{Guid.NewGuid()}")
            .Options;
        _dbContext = new TruLoadDbContext(dbOptions);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDocumentNumberService = new Mock<IDocumentNumberService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<WeighingService>>();

        // Setup default behavior for aggregation service
        _mockAxleGroupAggregationService
            .Setup(s => s.AggregateAxleGroupsAsync(It.IsAny<ICollection<WeighingAxle>>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<AxleGroupResultDto>());

        _mockAxleGroupAggregationService
            .Setup(s => s.DetermineAxleType(It.IsAny<int>(), It.IsAny<string>()))
            .Returns("SingleDrive");

        _service = new WeighingService(
            _mockWeighingRepository.Object,
            _mockAxleConfigurationRepository.Object,
            _mockPermitRepository.Object,
            _mockProhibitionRepository.Object,
            _mockToleranceRepository.Object,
            _mockFeeScheduleRepository.Object,
            _mockPdfService.Object,
            _mockBlobStorageService.Object,
            _mockDocumentRepository.Object,
            _mockAxleGroupAggregationService.Object,
            _mockScaleTestRepository.Object,
            _mockVehicleRepository.Object,
            _mockCaseRegisterService.Object,
            _mockYardService.Object,
            _mockVehicleTagService.Object,
            _dbContext,
            _mockSettingsService.Object,
            _mockDocumentNumberService.Object,
            _mockNotificationService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task InitiateWeighingAsync_ShouldCreateTransaction_WithPendingStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicleRegNo = "KAA001A";
        var generatedTicketNumber = "NRBM01-A-20260218-0001-KAA001A";

        // Seed a station so the orgId lookup query works
        _dbContext.Stations.Add(new TruLoad.Backend.Models.Station
        {
            Id = stationId,
            Code = "NRBM01",
            Name = "Test Station",
            OrganizationId = orgId,
        });
        await _dbContext.SaveChangesAsync();

        _mockScaleTestRepository.Setup(r => r.HasPassedDailyCalibrationalAsync(stationId, null))
            .ReturnsAsync(true);

        _mockDocumentNumberService.Setup(r => r.GenerateNumberAsync(
                orgId, stationId, It.IsAny<string>(), vehicleRegNo, null))
            .ReturnsAsync(generatedTicketNumber);

        _mockWeighingRepository.Setup(r => r.CreateTransactionAsync(It.IsAny<WeighingTransaction>()))
            .ReturnsAsync((WeighingTransaction t) => t);

        // Act
        var result = await _service.InitiateWeighingAsync(stationId, userId, vehicleId, vehicleRegNo);

        // Assert
        result.Should().NotBeNull();
        result.TicketNumber.Should().Be(generatedTicketNumber);
        result.StationId.Should().Be(stationId);
        result.WeighedByUserId.Should().Be(userId);
        result.VehicleId.Should().Be(vehicleId);
        result.VehicleRegNumber.Should().Be(vehicleRegNo);
        result.ControlStatus.Should().Be("Pending");
        result.CaptureStatus.Should().Be("pending");
        result.CaptureSource.Should().Be("frontend");
        result.WeighedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CaptureWeightsAsync_ShouldUpdateWeights_AndCalculateGvw()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var transaction = new WeighingTransaction { Id = transactionId, ControlStatus = "Pending" };
        
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, MeasuredWeightKg = 5000, AxleConfigurationId = configId },
            new WeighingAxle { AxleNumber = 2, MeasuredWeightKg = 6000, AxleConfigurationId = configId }
        };

        var config = new AxleConfiguration 
        { 
            Id = configId, 
            GvwPermissibleKg = 20000, 
            AxleWeightReferences = new List<AxleWeightReference>
            {
                new AxleWeightReference { AxlePosition = 1, AxleLegalWeightKg = 8000 },
                new AxleWeightReference { AxlePosition = 2, AxleLegalWeightKg = 8000 }
            }
        };

        _mockWeighingRepository.Setup(r => r.GetTransactionByIdAsync(transactionId))
            .ReturnsAsync(transaction);

        _mockWeighingRepository.Setup(r => r.UpdateTransactionAsync(It.IsAny<WeighingTransaction>()))
            .ReturnsAsync(transaction);

        _mockAxleConfigurationRepository.Setup(r => r.GetByIdAsync(configId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _service.CaptureWeightsAsync(transactionId, axles);

        // Assert
        result.WeighingAxles.Should().HaveCount(2);
        result.GvwMeasuredKg.Should().Be(11000); // 5000 + 6000
        result.ControlStatus.Should().Be("Compliant");
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateComplianceAsync_ShouldFlagOverload_WhenGvwExceeded()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, MeasuredWeightKg = 6000, AxleConfigurationId = configId },
            new WeighingAxle { AxleNumber = 2, MeasuredWeightKg = 6000, AxleConfigurationId = configId }
        };
        
        var transaction = new WeighingTransaction 
        { 
            Id = transactionId, 
            ControlStatus = "Pending",
            WeighingAxles = axles
        };

        var config = new AxleConfiguration 
        { 
            Id = configId, 
            GvwPermissibleKg = 10000, // Limit is 10k, measured is 12k
            AxleWeightReferences = new List<AxleWeightReference>
            {
                new AxleWeightReference { AxlePosition = 1, AxleLegalWeightKg = 8000 },
                new AxleWeightReference { AxlePosition = 2, AxleLegalWeightKg = 8000 }
            }
        };

        _mockWeighingRepository.Setup(r => r.GetTransactionByIdAsync(transactionId))
            .ReturnsAsync(transaction);
        
        _mockAxleConfigurationRepository.Setup(r => r.GetByIdAsync(configId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _service.CalculateComplianceAsync(transactionId);

        // Assert
        result.GvwMeasuredKg.Should().Be(12000);
        result.GvwPermissibleKg.Should().Be(10000);
        result.OverloadKg.Should().Be(2000);
        result.ControlStatus.Should().Be("Overloaded");
        result.IsCompliant.Should().BeFalse();
        result.IsSentToYard.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateComplianceAsync_ShouldAllowOperationalTolerance()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, MeasuredWeightKg = 5100, AxleConfigurationId = configId },
            new WeighingAxle { AxleNumber = 2, MeasuredWeightKg = 5050, AxleConfigurationId = configId }
        };
        // Total = 10150
        
        var transaction = new WeighingTransaction 
        { 
            Id = transactionId, 
            ControlStatus = "Pending",
            WeighingAxles = axles
        };

        var config = new AxleConfiguration 
        { 
            Id = configId, 
            GvwPermissibleKg = 10000,
            AxleWeightReferences = new List<AxleWeightReference>
            {
                new AxleWeightReference { AxlePosition = 1, AxleLegalWeightKg = 6000 },
                new AxleWeightReference { AxlePosition = 2, AxleLegalWeightKg = 6000 }
            }
        };

        _mockWeighingRepository.Setup(r => r.GetTransactionByIdAsync(transactionId))
            .ReturnsAsync(transaction);
        
        _mockAxleConfigurationRepository.Setup(r => r.GetByIdAsync(configId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _service.CalculateComplianceAsync(transactionId);

        // Assert
        result.GvwMeasuredKg.Should().Be(10150);
        result.OverloadKg.Should().Be(150); // < 200kg tolerance
        result.ControlStatus.Should().Be("Warning");
        result.IsCompliant.Should().BeFalse(); // Still strict non-compliant
        result.IsSentToYard.Should().BeFalse(); // But released
    }
}
