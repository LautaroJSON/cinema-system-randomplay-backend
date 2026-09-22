FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CinemaSystemRandomPlay.Api/CinemaSystemRandomPlay.Api.csproj src/CinemaSystemRandomPlay.Api/
COPY src/CinemaSystemRandomPlay.Application/CinemaSystemRandomPlay.Application.csproj src/CinemaSystemRandomPlay.Application/
COPY src/CinemaSystemRandomPlay.Domain/CinemaSystemRandomPlay.Domain.csproj src/CinemaSystemRandomPlay.Domain/
COPY src/CinemaSystemRandomPlay.Infrastructure/CinemaSystemRandomPlay.Infrastructure.csproj src/CinemaSystemRandomPlay.Infrastructure/
RUN dotnet restore src/CinemaSystemRandomPlay.Api/CinemaSystemRandomPlay.Api.csproj

COPY src/ src/
RUN dotnet publish src/CinemaSystemRandomPlay.Api/CinemaSystemRandomPlay.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "CinemaSystemRandomPlay.Api.dll"]
