#!/bin/sh

run_fuse() {
  for ((i=1; i < 10; i++))
  do
    dotnet CassandraFS/CassandraFS/bin/Debug/net6.0/CassandraFS.dll
    sleep 1
  done
}

run_tests() {
  for ((i=1; i < 10; i++))
  do
	dotnet test CassandraFS/FileSystemTests/FileSystemTests.csproj
	sleep 2
  done
}

mkdir /home/cassandra-fs

if [ $TESTING -eq 0 ]
then
  run_fuse &
  proftpd --nodaemon &
  run_tests
else
  run_fuse &
  proftpd --nodaemon
fi
# /run.sh -l puredb:/etc/pure-ftpd/pureftpd.pdb -E -j -R -P $PUBLICHOST
