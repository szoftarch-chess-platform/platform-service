# Builds for linux/amd64 and linux/arm64. The SDK stage always runs on the platform of the build machine
# and cross-compiles for TARGETARCH, so no emulation is needed. To build both architectures run
#   docker buildx build --platform linux/amd64,linux/arm64 -t platform-service .

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore in a separate layer, which only depends on the project and MSBuild files.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig dotnet-tools.json ./
COPY src/PlatformService.Domain/PlatformService.Domain.csproj src/PlatformService.Domain/
COPY src/PlatformService.Application/PlatformService.Application.csproj src/PlatformService.Application/
COPY src/PlatformService.Infrastructure/PlatformService.Infrastructure.csproj src/PlatformService.Infrastructure/
COPY src/PlatformService.Api/PlatformService.Api.csproj src/PlatformService.Api/
RUN dotnet restore src/PlatformService.Api/PlatformService.Api.csproj -a $TARGETARCH \
    && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/PlatformService.Api/PlatformService.Api.csproj -a $TARGETARCH --no-restore -c Release -o /app

# The migration bundle runs in a one-shot container of this image before the service starts (PS-ADR-7).
# It takes a runtime identifier, in which amd64 is called x64.
RUN case "$TARGETARCH" in amd64) rid=linux-x64 ;; *) rid="linux-$TARGETARCH" ;; esac \
    && dotnet ef migrations bundle --project src/PlatformService.Infrastructure \
        --configuration Release --target-runtime "$rid" --output /app/efbundle

# The chiseled image has no shell or package manager, so there is no HEALTHCHECK.
# The infra side checks the health from outside.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["./PlatformService.Api"]
