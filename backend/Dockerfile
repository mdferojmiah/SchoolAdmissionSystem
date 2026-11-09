FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY src/SchoolAdmission.Application/SchoolAdmission.Application.csproj src/SchoolAdmission.Application/
COPY src/SchoolAdmission.Domain/SchoolAdmission.Domain.csproj src/SchoolAdmission.Domain/
COPY src/SchoolAdmission.Infrastructure/SchoolAdmission.Infrastructure.csproj src/SchoolAdmission.Infrastructure/
RUN dotnet restore src/SchoolAdmission.Api/SchoolAdmission.Api.csproj

COPY . .
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
COPY . .
RUN dotnet restore tests/SchoolAdmission.Tests/SchoolAdmission.Tests.csproj
ENTRYPOINT ["dotnet", "test", "tests/SchoolAdmission.Tests/SchoolAdmission.Tests.csproj", "--no-restore", "--logger", "console;verbosity=normal"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
