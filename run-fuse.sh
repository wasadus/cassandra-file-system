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
	dotnet test CassandraFS/FileSystemTests/FileSystemTests.csproj
	sleep 2
  done
}

mkdir /home/cassandra-fs

if [ $TESTING -eq 1 ]
then
  run_fuse &
  proftpd --nodaemon &
  run_tests
else
  run_fuse &
  proftpd --nodaemon
fi
# /run.sh -l puredb:/etc/pure-ftpd/pureftpd.pdb -E -j -R -P $PUBLICHOST
