# CassandraFS

#### Proof-of-concept for FTP-server based on file system, writing directly to Cassandra. WIP.

## Сборка и запуск

Есть 3 возможных способа запуска:

- Все и сразу

  `build all.bat`

  Здесь в одном контейнере запускается и база данных, и файловая система

- Только база данных

  `build cassandra.bat`

  В контейнере находится только cassandra

- Только файловая система

  `build filesystem.bat`

  В контейнере лежит только файловая система

По умолчанию логи сохраняются в `logs/log` внутри контейнера
