FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# restore first, so this layer is cached until a .csproj changes
COPY API/API.csproj API/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore API/API.csproj
COPY API/ API/
COPY Domain/ Domain/
COPY Infrastructure/ Infrastructure/
RUN dotnet publish API/API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "API.dll"]