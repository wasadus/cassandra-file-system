#!/bin/sh

run_fuse() {
  while true;
  do
    dotnet CassandraFS/CassandraFS/bin/Debug/netcoreapp3.1/CassandraFS.dll /home/cassandra-fs
    sleep 1
  done
}

run_tests() {
  while true;
  do
	dotnet CassandraFS/StorageTests/bin/Debug/netcoreapp3.1/StorageTests.dll
	dotnet CassandraFS/FileSystemTests/bin/Debug/netcoreapp3.1/FileSystemTests.dll
	sleep 2
  done
}

mkdir /home/cassandra-fs

run_fuse &
proftpd --nodaemon &
run_tests
# /run.sh -l puredb:/etc/pure-ftpd/pureftpd.pdb -E -j -R -P $PUBLICHOST
