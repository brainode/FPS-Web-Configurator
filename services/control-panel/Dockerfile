FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY services/control-panel/control-panel.csproj services/control-panel/
RUN dotnet restore services/control-panel/control-panel.csproj

COPY services/control-panel/ services/control-panel/
WORKDIR /src/services/control-panel
RUN dotnet publish control-panel.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV PanelStorage__RootPath=/var/lib/control-panel
ENV PanelAuth__SeedAdminUsername=admin
ENV PanelAuth__SeedAdminPassword=change-me
ENV DockerAgent__BaseUrl=http://docker-agent:8081

COPY --from=build /app/publish .

VOLUME ["/var/lib/control-panel"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "control-panel.dll"]
