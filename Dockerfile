# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Spine.Api/Spine.Api.csproj", "Spine.Api/"]
COPY ["Spine.Core/Spine.Core.csproj", "Spine.Core/"]
COPY ["Spine.Infrastructure/Spine.Infrastructure.csproj", "Spine.Infrastructure/"]
RUN dotnet restore "Spine.Api/Spine.Api.csproj"
COPY . .
WORKDIR "/src/Spine.Api"
RUN dotnet publish -c Release -o /app

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render sets the PORT environment variable at runtime[reference:1]
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Spine.Api.dll"]