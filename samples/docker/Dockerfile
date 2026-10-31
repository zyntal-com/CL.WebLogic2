# syntax=docker/dockerfile:1.7
#
# One image recipe for both samples:  docker build -f samples/docker/Dockerfile --build-arg SAMPLE=MiniBlog .
# The toolkit is packed into a local NuGet feed first and the sample is published against that
# package, so the image proves the package works the way an app consumes it.

ARG DOTNET_VERSION=11.0

# Development only: build against unreleased CodeLogic / CodeLogic.Libs packed by
# build/pack-local-codelogic.ps1, with --build-context codelogic-local=artifacts/codelogic-local
# (compose: CODELOGIC_LOCAL_FEED=../../artifacts/codelogic-local). Empty by default: nuget.org.
FROM scratch AS codelogic-local

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG SAMPLE=StarterWebsite
WORKDIR /src
COPY . .
COPY --from=codelogic-local / /codelogic-local/
# A unique version per build, so the cached NuGet folder can never serve an older pack.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    VERSION="5.0.0-docker.$(date +%s)" \
 && LOCAL="" && if [ -f /codelogic-local/version.txt ]; then LOCAL="-p:CodeLogicLocal=true -p:CodeLogicLocalFeed=/codelogic-local"; fi \
 && dotnet pack src/CL.WebLogic/CL.WebLogic.csproj -c Release -o /feed -p:Version=$VERSION $LOCAL \
 && dotnet pack src/CL.WebLogic.Data/CL.WebLogic.Data.csproj -c Release -o /feed -p:Version=$VERSION $LOCAL \
 && dotnet pack src/CL.WebLogic.ExternalLogins/CL.WebLogic.ExternalLogins.csproj -c Release -o /feed -p:Version=$VERSION $LOCAL \
 && dotnet publish samples/${SAMPLE}/${SAMPLE}.csproj -c Release -o /app $LOCAL \
      -p:WebLogicFromPackage=true -p:WebLogicPackageVersion=$VERSION \
      --source /feed --source https://api.nuget.org/v3/index.json

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG SAMPLE=StarterWebsite
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    DEMO_DATA_DIR=/data \
    APP_DLL=${SAMPLE}.dll
WORKDIR /app
COPY --from=build /app .
# All state (CodeLogic config and logs, app data, plugins, DataProtection keys) lives in /data.
RUN mkdir -p /data && chown app:app /data
USER app
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=8s --start-period=40s --retries=3 CMD dotnet /app/$APP_DLL --probe
ENTRYPOINT exec dotnet /app/$APP_DLL
