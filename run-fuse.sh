#!/bin/sh

run_fuse() {
  while true;
  do
    dotnet CassandraFS/CassandraFS/bin/Debug/netcoreapp3.1/CassandraFS.dll /home/cassandra-fs
    sleep 1
  done
}

mkdir /home/cassandra-fs

run_fuse &
proftpd --nodaemon
# /run.sh -l puredb:/etc/pure-ftpd/pureftpd.pdb -E -j -R -P $PUBLICHOST
