FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY GitTracker/*.csproj ./GitTracker/
COPY GitTracker.Tests/*.csproj ./GitTracker.Tests/
RUN dotnet restore GitTracker.sln

COPY ./ ./
RUN dotnet build GitTracker.sln

ENTRYPOINT ["dotnet", "test", "GitTracker.sln"]