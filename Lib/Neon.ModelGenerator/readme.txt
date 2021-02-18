  <Target Name="ModelGen" BeforeTargets="BeforeBuild">
    <Exec Command="dotnet publish &quot;%NF_ROOT%\Tools\neon-modelgen\neon-modelgen.csproj&quot; --framework netcoreapp3.1 --output &quot;%NF_ROOT%\Lib\Neon.ModelGenerator\neon-modelgen\linux-x64&quot; --configuration Release --runtime linux-x64" />
    <Exec Command="dotnet publish &quot;%NF_ROOT%\Tools\neon-modelgen\neon-modelgen.csproj&quot; --framework netcoreapp3.1 --output &quot;%NF_ROOT%\Lib\Neon.ModelGenerator\neon-modelgen\osx-x64&quot; --configuration Release --runtime osx-x64" />
    <Exec Command="dotnet publish &quot;%NF_ROOT%\Tools\neon-modelgen\neon-modelgen.csproj&quot; --framework netcoreapp3.1 --output &quot;%NF_ROOT%\Lib\Neon.ModelGenerator\neon-modelgen\win-x64&quot; --configuration Release --runtime win-x64" />
  </Target>

