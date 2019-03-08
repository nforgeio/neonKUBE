FROM microsoft/dotnet:3.0-aspnetcore-runtime-stretch-slim AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:3.0-sdk-stretch AS build
WORKDIR /src
COPY ["Test/Test.RestApi/Test.RestApi.csproj", "Test/Test.RestApi/"]
RUN dotnet restore "Test/Test.RestApi/Test.RestApi.csproj"
COPY . .
WORKDIR "/src/Test/Test.RestApi"
RUN dotnet build "Test.RestApi.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Test.RestApi.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Test.RestApi.dll"]