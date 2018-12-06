ssh pi@jnm.gotdns.com sudo systemctl stop sharexapi
scp -r ./bin/Release/netcoreapp2.1/linux-arm/publish/* pi@jnm.gotdns.com:/var/www/sharexapi/bin
ssh pi@jnm.gotdns.com sudo systemctl start sharexapi
