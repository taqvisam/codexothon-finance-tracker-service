FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PersonalFinanceTracker.sln ./
COPY src/PersonalFinanceTracker.Api/PersonalFinanceTracker.Api.csproj src/PersonalFinanceTracker.Api/
COPY src/PersonalFinanceTracker.Application/PersonalFinanceTracker.Application.csproj src/PersonalFinanceTracker.Application/
COPY src/PersonalFinanceTracker.Domain/PersonalFinanceTracker.Domain.csproj src/PersonalFinanceTracker.Domain/
COPY src/PersonalFinanceTracker.Infrastructure/PersonalFinanceTracker.Infrastructure.csproj src/PersonalFinanceTracker.Infrastructure/
RUN dotnet restore PersonalFinanceTracker.sln

COPY src ./src
RUN dotnet publish src/PersonalFinanceTracker.Api/PersonalFinanceTracker.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "PersonalFinanceTracker.Api.dll"]
