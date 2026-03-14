FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY KryskataFund/KryskataFund.csproj KryskataFund/
RUN dotnet restore KryskataFund/KryskataFund.csproj
COPY KryskataFund/ KryskataFund/
RUN dotnet publish KryskataFund/KryskataFund.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "KryskataFund.dll"]
