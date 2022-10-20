#!/bin/sh

run_fuse() {
  while true;
  do
    dotnet CassandraFS/CassandraFS/bin/Debug/net6.0/CassandraFS.dll
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
