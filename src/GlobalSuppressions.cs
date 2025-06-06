using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", 
    Justification = "Builder pattern requires nested types")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", 
    Justification = "Logging is not performance critical in this library")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", 
    Justification = "Arguments are validated with ArgumentNullException where appropriate")]
