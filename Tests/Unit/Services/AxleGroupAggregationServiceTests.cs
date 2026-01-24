using FluentAssertions;
using Moq;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Implementations.Weighing;
using Xunit;

namespace Truload.Backend.Tests.Unit.Services;

/// <summary>
/// Unit tests for AxleGroupAggregationService.
/// Tests regulatory compliance with Kenya Traffic Act Cap 403 and EAC Act 2016.
/// </summary>
public class AxleGroupAggregationServiceTests
{
    private readonly Mock<IWeighingRepository> _mockWeighingRepository;
    private readonly Mock<IToleranceRepository> _mockToleranceRepository;
    private readonly Mock<IAxleTypeFeeRepository> _mockAxleTypeFeeRepository;
    private readonly Mock<IAxleFeeScheduleRepository> _mockGvwFeeRepository;
    private readonly Mock<IDemeritPointsRepository> _mockDemeritRepository;
    private readonly AxleGroupAggregationService _service;

    public AxleGroupAggregationServiceTests()
    {
        _mockWeighingRepository = new Mock<IWeighingRepository>();
        _mockToleranceRepository = new Mock<IToleranceRepository>();
        _mockAxleTypeFeeRepository = new Mock<IAxleTypeFeeRepository>();
        _mockGvwFeeRepository = new Mock<IAxleFeeScheduleRepository>();
        _mockDemeritRepository = new Mock<IDemeritPointsRepository>();

        _service = new AxleGroupAggregationService(
            _mockWeighingRepository.Object,
            _mockToleranceRepository.Object,
            _mockAxleTypeFeeRepository.Object,
            _mockGvwFeeRepository.Object,
            _mockDemeritRepository.Object
        );
    }

    #region Tolerance Tests

    [Fact]
    public async Task AggregateAxleGroupsAsync_SingleAxleGroup_ShouldApply5PercentTolerance()
    {
        // Arrange - Single steering axle (Group A)
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, AxleGrouping = "A", MeasuredWeightKg = 7350, PermissibleWeightKg = 7000 }
        };

        _mockAxleTypeFeeRepository.Setup(r => r.CalculateFeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(0m);

        _mockDemeritRepository.Setup(r => r.CalculatePointsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.Should().HaveCount(1);
        var groupA = result.First();
        groupA.GroupLabel.Should().Be("A");
        groupA.AxleType.Should().Be("Steering");
        groupA.ToleranceKg.Should().Be(350); // 5% of 7000 = 350
        groupA.EffectiveLimitKg.Should().Be(7350); // 7000 + 350
        groupA.OverloadKg.Should().Be(0); // 7350 <= 7350
        groupA.Status.Should().Be("LEGAL");
    }

    [Fact]
    public async Task AggregateAxleGroupsAsync_TandemGroup_ShouldApplyZeroTolerance()
    {
        // Arrange - Tandem axles (Group B with 2 axles)
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 2, AxleGrouping = "B", MeasuredWeightKg = 8500, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 3, AxleGrouping = "B", MeasuredWeightKg = 8300, PermissibleWeightKg = 8000 }
        };

        _mockAxleTypeFeeRepository.Setup(r => r.CalculateFeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(100m);

        _mockDemeritRepository.Setup(r => r.CalculatePointsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.Should().HaveCount(1);
        var groupB = result.First();
        groupB.GroupLabel.Should().Be("B");
        groupB.AxleType.Should().Be("Tandem");
        groupB.ToleranceKg.Should().Be(0); // 0% for grouped axles
        groupB.EffectiveLimitKg.Should().Be(16000); // 16000 + 0
        groupB.OverloadKg.Should().Be(800); // 16800 - 16000
        groupB.Status.Should().Be("OVERLOAD"); // 800 > 200
    }

    [Fact]
    public async Task AggregateAxleGroupsAsync_TridemGroup_ShouldApplyZeroTolerance()
    {
        // Arrange - Tridem axles (Group C with 3 axles)
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 4, AxleGrouping = "C", MeasuredWeightKg = 8200, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 5, AxleGrouping = "C", MeasuredWeightKg = 8100, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 6, AxleGrouping = "C", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 }
        };

        _mockAxleTypeFeeRepository.Setup(r => r.CalculateFeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(50m);

        _mockDemeritRepository.Setup(r => r.CalculatePointsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.Should().HaveCount(1);
        var groupC = result.First();
        groupC.GroupLabel.Should().Be("C");
        groupC.AxleType.Should().Be("Tridem");
        groupC.ToleranceKg.Should().Be(0); // 0% for grouped axles
        groupC.OverloadKg.Should().Be(300); // 24300 - 24000
        groupC.Status.Should().Be("OVERLOAD"); // 300 > 200
    }

    #endregion

    #region Pavement Damage Factor Tests

    [Fact]
    public void CalculatePavementDamageFactor_ExactLimit_ShouldReturnOne()
    {
        // Act
        var pdf = _service.CalculatePavementDamageFactor(10000, 10000);

        // Assert
        pdf.Should().Be(1.0m);
    }

    [Fact]
    public void CalculatePavementDamageFactor_5PercentOver_ShouldReturn121Percent()
    {
        // Act
        var pdf = _service.CalculatePavementDamageFactor(10500, 10000);

        // Assert - 1.05^4 = 1.2155
        pdf.Should().BeApproximately(1.2155m, 0.001m);
    }

    [Fact]
    public void CalculatePavementDamageFactor_10PercentOver_ShouldReturn146Percent()
    {
        // Act
        var pdf = _service.CalculatePavementDamageFactor(11000, 10000);

        // Assert - 1.1^4 = 1.4641
        pdf.Should().BeApproximately(1.4641m, 0.001m);
    }

    [Fact]
    public void CalculatePavementDamageFactor_ZeroPermissible_ShouldReturnZero()
    {
        // Act
        var pdf = _service.CalculatePavementDamageFactor(10000, 0);

        // Assert
        pdf.Should().Be(0m);
    }

    #endregion

    #region Axle Type Determination Tests

    [Fact]
    public void DetermineAxleType_SingleAxleGroupA_ShouldReturnSteering()
    {
        var axleType = _service.DetermineAxleType(1, "A");
        axleType.Should().Be("Steering");
    }

    [Fact]
    public void DetermineAxleType_SingleAxleGroupB_ShouldReturnSingleDrive()
    {
        var axleType = _service.DetermineAxleType(1, "B");
        axleType.Should().Be("SingleDrive");
    }

    [Fact]
    public void DetermineAxleType_TwoAxles_ShouldReturnTandem()
    {
        var axleType = _service.DetermineAxleType(2, "B");
        axleType.Should().Be("Tandem");
    }

    [Fact]
    public void DetermineAxleType_ThreeAxles_ShouldReturnTridem()
    {
        var axleType = _service.DetermineAxleType(3, "C");
        axleType.Should().Be("Tridem");
    }

    [Fact]
    public void DetermineAxleType_FourAxles_ShouldReturnQuad()
    {
        var axleType = _service.DetermineAxleType(4, "D");
        axleType.Should().Be("Quad");
    }

    #endregion

    #region Status Determination Tests

    [Fact]
    public async Task AggregateAxleGroupsAsync_WithinLimit_ShouldReturnLegal()
    {
        // Arrange
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, AxleGrouping = "A", MeasuredWeightKg = 6500, PermissibleWeightKg = 7000 }
        };

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.First().Status.Should().Be("LEGAL");
        result.First().OverloadKg.Should().Be(0);
    }

    [Fact]
    public async Task AggregateAxleGroupsAsync_WithinOperationalTolerance_ShouldReturnWarning()
    {
        // Arrange - Single axle overloaded by 150kg (within 200kg operational tolerance)
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, AxleGrouping = "A", MeasuredWeightKg = 7500, PermissibleWeightKg = 7000 }
        };
        // 5% tolerance = 350kg, effective limit = 7350kg
        // Overload = 7500 - 7350 = 150kg (within 200kg operational tolerance)

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.First().Status.Should().Be("WARNING");
        result.First().OverloadKg.Should().Be(150);
    }

    [Fact]
    public async Task AggregateAxleGroupsAsync_ExceedsOperationalTolerance_ShouldReturnOverload()
    {
        // Arrange - Single axle overloaded by 500kg (exceeds 200kg operational tolerance)
        var axles = new List<WeighingAxle>
        {
            new WeighingAxle { AxleNumber = 1, AxleGrouping = "A", MeasuredWeightKg = 7850, PermissibleWeightKg = 7000 }
        };
        // 5% tolerance = 350kg, effective limit = 7350kg
        // Overload = 7850 - 7350 = 500kg (exceeds 200kg operational tolerance)

        _mockAxleTypeFeeRepository.Setup(r => r.CalculateFeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(100m);

        _mockDemeritRepository.Setup(r => r.CalculatePointsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.First().Status.Should().Be("OVERLOAD");
        result.First().OverloadKg.Should().Be(500);
    }

    #endregion

    #region Mixed Vehicle Tests

    [Fact]
    public async Task AggregateAxleGroupsAsync_MixedVehicle_ShouldProcessAllGroups()
    {
        // Arrange - Typical truck with steering + tandem + tridem
        var axles = new List<WeighingAxle>
        {
            // Group A - Steering (single)
            new WeighingAxle { AxleNumber = 1, AxleGrouping = "A", MeasuredWeightKg = 6800, PermissibleWeightKg = 7000 },
            // Group B - Tandem (2 axles)
            new WeighingAxle { AxleNumber = 2, AxleGrouping = "B", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 3, AxleGrouping = "B", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 },
            // Group C - Tridem (3 axles)
            new WeighingAxle { AxleNumber = 4, AxleGrouping = "C", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 5, AxleGrouping = "C", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 },
            new WeighingAxle { AxleNumber = 6, AxleGrouping = "C", MeasuredWeightKg = 8000, PermissibleWeightKg = 8000 }
        };

        // Act
        var result = await _service.AggregateAxleGroupsAsync(axles, "TRAFFIC_ACT", 200);

        // Assert
        result.Should().HaveCount(3);

        result.Single(g => g.GroupLabel == "A").AxleType.Should().Be("Steering");
        result.Single(g => g.GroupLabel == "B").AxleType.Should().Be("Tandem");
        result.Single(g => g.GroupLabel == "C").AxleType.Should().Be("Tridem");

        // All groups within limits
        result.All(g => g.Status == "LEGAL").Should().BeTrue();
    }

    #endregion
}
