# CassandraFS

#### Proof-of-concept for FTP-server based on file system, writing directly to Cassandra. WIP.

## Настройка оружения

- Включить виртуализацию в биосе
- Включить подсистему Windows для Linux
  `dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart`
- Включить компоненту виртуальных машин
  `dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart`
- Скачиваем и ставим пакет обновления ядра Linux
  https://wslstorestorage.blob.core.windows.net/wslblob/wsl_update_x64.msi
- Выбираем WSL 2 в качестве версии по умолчанию
  `wsl --set-default-version 2`
- `wsl --install -d Ubuntu-20.04`
  (Логин, пароль какие хочется)
- важно убедиться что установленный дистрибутив является дистрибутивом по умолчанию
  `wsl --list`
  для Ubuntu-20.04 вывод должен содержать строчку `Ubuntu-20.04 (Default)`
  если слова Default нет, то выполнить команду `wsl --set-default Ubuntu-20.04`
- Установить docker-compose
  https://docs.docker.com/compose/install/other/
- Также можно поставить Docker desktop

## Сборка и запуск

Есть 3 возможных способа запуска:

- Все и сразу

  `build all.bat`

  Запускается и база данных, и файловая система

- Только база данных

  `build cassandra.bat`

  Запускается контейнер cassandra

- Только файловая система

  `build filesystem.bat`

  Запускается контейнер с файловой системой (но он требует кассандру)

По умолчанию логи сохраняются в `logs/log` внутри контейнера

## Как посмотреть что что-то работает

- Поднять контейнеры, запустив `build all.bat`
- Зайти в контейнер filesystem, пройти по пути из переменной MountPointPath (лежит в config.json), записать там файл или созать еще папки
- В кассандре (внутри контейнера cassandra) в соответствующих таблицах появятся записи о файлах и папках. К бд можно подключиться например через райдер на стандратный порт (Database -> New -> Data Source -> Apache Cassandra) (нужный порт пробрасывается из контейнера наружу) (user,password можно пустые)
