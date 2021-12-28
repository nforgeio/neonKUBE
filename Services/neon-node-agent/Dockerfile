# Build the operator
FROM mcr.microsoft.com/dotnet/sdk:latest as build
WORKDIR /operator

COPY ./ ./
RUN dotnet publish -c Release -o out Services/neon-node-agent/neon-node-agent.csproj

# The runner for the application
FROM mcr.microsoft.com/dotnet/aspnet:latest as final

RUN addgroup k8s-operator && useradd -G k8s-operator operator-user

WORKDIR /operator
COPY --from=build /operator/out/ ./
RUN chown operator-user:k8s-operator -R .

USER operator-user

ENTRYPOINT [ "dotnet", "neon-node-agent.dll" ]
