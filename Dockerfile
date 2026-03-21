FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KryskataFund/KryskataFund.csproj", "KryskataFund/"]
RUN dotnet restore "KryskataFund/KryskataFund.csproj"
COPY . .
WORKDIR "/src/KryskataFund"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KryskataFund.dll"]
