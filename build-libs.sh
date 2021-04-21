#!/bin/sh

assert_exists () {
  if ! type "$1" 2> /dev/null 1>&2 ; then
    echo "Error: '$1' not installed"
    exit 1
  fi
}

init_configuration () {
    if [ ! -z $1 ] 
    then 
        CONFIGURATION="$1"
    else
        CONFIGURATION="Debug"
    fi
}

build_fs () {
    dotnet build CassandraFS/CassandraFS/CassandraFS.csproj -c "$CONFIGURATION"
}

build_tests () {
	dotnet build CassandraFS/StorageTests/StorageTests.csproj -c "$CONFIGURATION"
	dotnet build CassandraFS/FileSystemTests/FileSystemTests.csproj -c "$CONFIGURATION"
}

assert_exists dotnet
echo "dotnet ok"

init_configuration
echo "configuration: $CONFIGURATION"

echo "building fs..."
build_fs
err = $?
if [ err -eq 0 ]
then
	echo "building fs ok"
else
	echo "building fs failed"
	return $err
fi

echo "building tests..."
build_tests
err = $?

if [ err -eq 0 ]
then
	echo "building tests ok"
else
	echo "building tests failed"
fi
return $err