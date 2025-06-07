# Contributing to Relay

Thank you for considering contributing to Relay! This document outlines the process for contributing to the project and how to get started.

## Code of Conduct

By participating in this project, you agree to uphold our Code of Conduct, which expects all participants to be respectful, considerate, and constructive in their interactions.

## How Can I Contribute?

### Reporting Bugs

If you find a bug, please create an issue in the GitHub repository with the following information:

- A clear and descriptive title
- Detailed steps to reproduce the issue
- Expected behavior
- Actual behavior
- Screenshots (if applicable)
- Environment information (OS, .NET version, etc.)

### Suggesting Features

If you have an idea for a new feature or improvement, please create an issue with:

- A clear and descriptive title
- A detailed description of the proposed feature
- Any relevant examples or use cases
- If possible, an outline of how the feature might be implemented

### Pull Requests

We welcome pull requests! Here's how to submit one:

1. Fork the repository
2. Create a new branch from `main` with a descriptive name:
   ```
   git checkout -b feature/your-feature-name
   ```
   or
   ```
   git checkout -b fix/issue-you-are-fixing
   ```
3. Make your changes
4. Add or update tests as necessary
5. Ensure all tests pass
6. Update documentation if needed
7. Push your branch to your fork
8. Open a pull request against the `main` branch

## Development Environment Setup

1. Install the latest [.NET SDK](https://dotnet.microsoft.com/download)
2. Clone the repository:
   ```
   git clone https://github.com/TarasKovalenko/CentralConfigGenerator.git
   cd CentralConfigGenerator
   ```
3. Build the project:
   ```
   dotnet build
   ```
4. Run the tests:
   ```
   dotnet test
   ```

## Coding Style and Guidelines

- Follow the established coding style in the project
- Write clear, meaningful commit messages
- Include XML documentation comments for public APIs
- Maintain existing test coverage and add tests for new functionality
- Use meaningful variable and method names that reflect their purpose

## Versioning

This project follows [Semantic Versioning](https://semver.org/). When suggesting changes, consider whether they are:

- **PATCH** (1.0.x) - Bug fixes and minor changes that don't affect the API
- **MINOR** (1.x.0) - New features that don't break backward compatibility
- **MAJOR** (x.0.0) - Changes that break backward compatibility

## Documentation

When adding new features or changing existing functionality, please update the documentation:

- Update the README.md if necessary
- Add or update XML documentation comments for public APIs
- Update or add examples if relevant

## Testing

- Write unit tests for new functionality using the project's chosen testing framework
- Ensure all tests pass before submitting your pull request
- Try to maintain or improve the existing test coverage

## Feedback

Your feedback is valuable! If you have any questions or suggestions about the contribution process, please open an issue to discuss it.

Thank you for helping improve CentralConfigGenerator!
