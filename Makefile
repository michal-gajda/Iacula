build:
	dotnet build src/WebApi

publish:
	dotnet publish src/WebApi --configuration Release --output ./publish1
	dotnet publish src/WebApi --configuration Release --output ./publish2

run:
	dotnet run --project src/WebApi

test:
	dotnet test
