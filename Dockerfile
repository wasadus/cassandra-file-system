FROM kibatic/proftpd@sha256:2965544d873d20b22ca9e9e4853bf56bac2351f3e1acc50c4c34e64e849f2ea2

WORKDIR /app

RUN apt-get update
RUN apt-get install apt-transport-https -y
RUN apt-get update
RUN apt install libtool -y
RUN apt-get install -y libtool-bin
RUN apt install wget -y
RUN wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update

RUN apt-get update && apt-get install -y --no-install-recommends apt-utils
RUN apt-get upgrade -y
RUN apt-get update --fix-missing
RUN apt-get install -y dotnet-sdk-6.0
RUN apt-get install -y aspnetcore-runtime-6.0
RUN apt-get install -y fuse
RUN apt-get install -y libfuse-dev

RUN apt-get install libgtk2.0-dev -y
RUN apt-get install libglib2.0-dev -y

RUN apt-get -y install sudo

ADD . .
RUN chmod +x build-libs.sh
RUN chmod +x run-fuse.sh
RUN sh build-libs.sh
COPY proftpd.conf /etc/proftpd/proftpd.conf
CMD ./run-fuse.sh
