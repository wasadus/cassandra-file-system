FROM kibatic/proftpd@sha256:2965544d873d20b22ca9e9e4853bf56bac2351f3e1acc50c4c34e64e849f2ea2

WORKDIR /app

RUN apt update \
  && apt install apt-transport-https -y \
  && apt update \
  && apt install libtool -y \
  && apt install -y libtool-bin \
  && apt install wget -y \
  && wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
  && dpkg -i packages-microsoft-prod.deb \
  && rm packages-microsoft-prod.deb \
  && apt update \
  && apt install -y --no-install-recommends apt-utils \
  && apt upgrade -y \
  && apt update --fix-missing

RUN apt install -y \
    dotnet-sdk-6.0 \
    aspnetcore-runtime-6.0 \
    fuse \
    libfuse-dev \
    libgtk2.0-dev \
    libglib2.0-dev

RUN apt-get -y install sudo

COPY . .
RUN sh build-libs.sh 
    
COPY proftpd.conf /etc/proftpd/proftpd.conf
CMD sh run-fuse.sh
