#!/bin/sh

run_fuse() {
  while true;
  do
    dotnet CassandraFS/CassandraFS/bin/Debug/net6.0/CassandraFS.dll
    sleep 1
  done
}

run_tests() {
#  for ((i=1; i < 2; i++))
#  do
    #sleep 10
    #dotnet test CassandraFS/FileSystemTests/FileSystemTests.csproj --filter Name=TestFilesInDeletedDirectoryAreDeleted_WhenRecursive
    dotnet test CassandraFS/FileSystemTests/FileSystemTests.csproj
    #sleep 2
    
#  done
#TODO: Запускать все тесты (сделать следовало вчера)
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
