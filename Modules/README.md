# Modules

This folder contains the modular feature implementations following a clean architecture approach.

## Structure

Each module is self-contained with its own entities, DTOs, services, and controllers:

```
Modules/
├── UserManagement/
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Role.cs
│   │   └── Shift.cs
│   ├── DTOs/
│   │   ├── UserDto.cs
│   │   ├── LoginRequest.cs
│   │   └── RegisterRequest.cs
│   ├── Services/
│   │   ├── IUserService.cs
│   │   └── UserService.cs
│   ├── Controllers/
│   │   └── UsersController.cs
│   └── README.md
│
├── Weighing/
│   ├── Entities/
│   │   ├── Vehicle.cs
│   │   ├── WeighSession.cs
│   │   ├── WeighReading.cs
│   │   └── ScaleTest.cs
│   ├── DTOs/
│   │   ├── WeighSessionDto.cs
│   │   ├── StartWeighingRequest.cs
│   │   └── TakeWeightRequest.cs
│   ├── Services/
│   │   ├── IWeighingService.cs
│   │   ├── WeighingService.cs
│   │   ├── ITruConnectClient.cs
│   │   └── TruConnectClient.cs
│   ├── Controllers/
│   │   └── WeighingController.cs
│   └── README.md
│
├── Prosecution/
│   ├── Entities/
│   │   ├── ProsecutionCase.cs
│   │   ├── ProhibitionOrder.cs
│   │   ├── LoadCorrectionMemo.cs
│   │   └── ComplianceCertificate.cs
│   ├── DTOs/
│   │   ├── ProsecutionCaseDto.cs
│   │   ├── CaseDetailsRequest.cs
│   │   └── ChargeCalculationDto.cs
│   ├── Services/
│   │   ├── IProsecutionService.cs
│   │   ├── ProsecutionService.cs
│   │   ├── IChargeCalculator.cs
│   │   ├── EacActChargeCalculator.cs
│   │   └── TrafficActChargeCalculator.cs
│   ├── Controllers/
│   │   └── ProsecutionController.cs
│   └── README.md
│
├── SpecialRelease/
│   ├── Entities/
│   │   ├── SpecialRelease.cs
│   │   └── Permit.cs
│   ├── DTOs/
│   ├── Services/
│   ├── Controllers/
│   └── README.md
│
├── VehicleInspection/
│   ├── Entities/
│   ├── DTOs/
│   ├── Services/
│   ├── Controllers/
│   └── README.md
│
├── Yard/
│   ├── Entities/
│   ├── DTOs/
│   ├── Services/
│   ├── Controllers/
│   └── README.md
│
├── Settings/
│   ├── Entities/
│   ├── DTOs/
│   ├── Services/
│   ├── Controllers/
│   └── README.md
│
└── Reporting/
    ├── DTOs/
    ├── Services/
    ├── Controllers/
    └── README.md
```

## Module Design Principles

### 1. **Self-Contained**
Each module should be as independent as possible, with minimal coupling to other modules.

### 2. **Vertical Slicing**
Organize by feature (module) rather than by technical layer. Each module contains all layers it needs.

### 3. **Entities**
Domain models that map to database tables. Should contain:
- Properties with proper data types
- Navigation properties for relationships
- Business logic methods (if using rich domain models)
- Data annotations or Fluent API configurations

### 4. **DTOs (Data Transfer Objects)**
Objects for API requests and responses:
- Input DTOs for request bodies
- Output DTOs for responses
- Validation attributes
- Should NOT expose internal entity structure directly

### 5. **Services**
Business logic layer:
- Interface (`IXxxService`) + Implementation (`XxxService`)
- Contains core business rules
- Orchestrates data access via repositories or DbContext
- Handles calculations, validations, external integrations
- Returns DTOs, not entities

### 6. **Controllers**
API endpoints:
- Thin controllers - delegate to services
- Handle HTTP concerns (routing, status codes, validation)
- Use DTOs for input/output
- Include XML documentation for Swagger

## Example Module: Weighing

```csharp
// Entities/WeighSession.cs
public class WeighSession
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; }
    public DateTime WeighDate { get; set; }
    public int AxleConfigurationId { get; set; }
    public decimal GrossVehicleWeight { get; set; }
    public bool IsCompliant { get; set; }
    
    public AxleConfiguration AxleConfiguration { get; set; }
    public ICollection<WeighReading> Readings { get; set; }
}

// DTOs/StartWeighingRequest.cs
public class StartWeighingRequest
{
    [Required]
    public string RegistrationNumber { get; set; }
    
    [Required]
    public int AxleConfigurationId { get; set; }
    
    public string Bound { get; set; } = "A";
}

// Services/IWeighingService.cs
public interface IWeighingService
{
    Task<WeighSessionDto> StartWeighingAsync(StartWeighingRequest request);
    Task<WeightDataDto> GetWeightFromTruConnectAsync();
    Task<WeighSessionDto> TakeWeightAsync(int sessionId, TakeWeightRequest request);
    Task<WeighTicketDto> FinalizeWeighingAsync(int sessionId);
}

// Controllers/WeighingController.cs
[ApiController]
[Route("api/[controller]")]
public class WeighingController : ControllerBase
{
    private readonly IWeighingService _weighingService;
    
    public WeighingController(IWeighingService weighingService)
    {
        _weighingService = weighingService;
    }
    
    [HttpPost("start")]
    public async Task<ActionResult<WeighSessionDto>> StartWeighing(
        [FromBody] StartWeighingRequest request)
    {
        var result = await _weighingService.StartWeighingAsync(request);
        return Ok(result);
    }
}
```

## Inter-Module Communication

Modules may need to interact. Options:

1. **Direct Service Injection** (for tightly coupled modules)
   ```csharp
   public class ProsecutionService
   {
       private readonly IWeighingService _weighingService;
   }
   ```

2. **Events/Messages** (for loosely coupled modules)
   ```csharp
   // Publish event when weighing completes
   await _eventBus.PublishAsync(new WeighingCompletedEvent { SessionId = sessionId });
   
   // Subscribe in Prosecution module
   public class WeighingCompletedHandler : IEventHandler<WeighingCompletedEvent>
   ```

3. **Shared Kernel** (for common domain concepts)
   ```
   Shared/
   ├── Domain/
   │   ├── ValueObjects/
   │   └── Enums/
   ```

## Module Implementation Order

Based on the implementation plan, develop modules in this order:

1. **User Management** (Sprint 1-2)
2. **Settings** (Sprint 2-3) - Required for other modules
3. **Weighing** (Sprint 4-7) - Core module
4. **Yard** (Sprint 8-9)
5. **Prosecution** (Sprint 10-12)
6. **Special Release** (Sprint 13)
7. **Vehicle Inspection** (Sprint 14)
8. **Reporting** (Sprint 15-16)

## Testing

Each module should have corresponding tests:
```
Tests/
├── UserManagement.Tests/
├── Weighing.Tests/
├── Prosecution.Tests/
└── ...
```

## Documentation

Each module should have its own README explaining:
- Module purpose
- Key entities
- Business rules
- API endpoints
- Dependencies on other modules
- Configuration requirements

