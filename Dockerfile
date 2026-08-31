FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["GymSaaS.sln", "./"]
COPY ["src/GymSaaS.Domain/GymSaaS.Domain.csproj", "src/GymSaaS.Domain/"]
COPY ["src/GymSaaS.Application/GymSaaS.Application.csproj", "src/GymSaaS.Application/"]
COPY ["src/GymSaaS.Infrastructure/GymSaaS.Infrastructure.csproj", "src/GymSaaS.Infrastructure/"]
COPY ["src/GymSaaS.API/GymSaaS.API.csproj", "src/GymSaaS.API/"]

RUN dotnet restore "src/GymSaaS.API/GymSaaS.API.csproj"

COPY . .
WORKDIR "/src/src/GymSaaS.API"
RUN dotnet build "GymSaaS.API.csproj" -c Release -o /app/build
RUN dotnet publish "GymSaaS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GymSaaS.API.dll"]
