build:
	dotnet build src/WebApi

publish:
	dotnet publish src/WebApi --configuration Release --output ./publish1
	dotnet publish src/WebApi --configuration Release --output ./publish2

run:
	dotnet --project src/WebApi run

test:
	dotnet test

whatch:
	dotnet watch --project src/WebApi run
