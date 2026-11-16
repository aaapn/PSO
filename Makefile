.PHONY: all build test demo run clean

# Те же команды, что в C++ версии. Без make: dotnet run --project tests и т.д.

all: build

build:
	dotnet build Pos.sln

test:
	dotnet run --project tests

demo:
	dotnet run --project src/composition -- --demo

run:
	dotnet run --project src/composition

clean:
	dotnet clean Pos.sln
	rm -rf src/*/bin src/*/obj tests/bin tests/obj testdata_pos
