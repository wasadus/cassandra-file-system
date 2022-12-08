FROM kibatic/proftpd@sha256:2965544d873d20b22ca9e9e4853bf56bac2351f3e1acc50c4c34e64e849f2ea2

WORKDIR /app

RUN apt-get update \
  && apt-get install apt-transport-https -y \
  && apt-get update \
  && apt install libtool -y \
  && apt-get install -y libtool-bin \
  && apt install wget -y \
  && wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
  && dpkg -i packages-microsoft-prod.deb \
  && rm packages-microsoft-prod.deb \
  && apt-get update \
  && apt-get install -y --no-install-recommends apt-utils \
  && apt-get upgrade -y \
  && apt-get update --fix-missing

RUN apt-get install -y dotnet-sdk-6.0 \
  && apt-get install -y aspnetcore-runtime-6.0 \
  && apt-get install -y fuse \
  && apt-get install -y libfuse-dev \
  && apt-get install libgtk2.0-dev -y \
  && apt-get install libglib2.0-dev -y

RUN apt-get -y install sudo

ADD . .
RUN chmod +x build-libs.sh \
  && chmod +x run-fuse.sh \
  && sh build-libs.sh 
    
COPY proftpd.conf /etc/proftpd/proftpd.conf
CMD ./run-fuse.sh
