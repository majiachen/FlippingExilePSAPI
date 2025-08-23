FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Clean any previous artifacts
RUN rm -rf obj bin

# Copy project file and restore
COPY ["FlippingExilesPublicStashAPI.csproj", "."]
RUN dotnet restore "FlippingExilesPublicStashAPI.csproj"

# Copy source code (obj/bin are excluded by .dockerignore)
COPY . .

# Build with explicit configuration and no build cache issues
RUN dotnet build "FlippingExilesPublicStashAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FlippingExilesPublicStashAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-build --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlippingExilesPublicStashAPI.dll"]