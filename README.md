# Nmap-TCP-ICMP-pinger

Lightweight optimized pinging tool for TCP &amp; ICMP protocols, aswell as a built-in TCP Nmap scanner. Written in C# ❤️

##Options

1. ICMP - Sends an infinite amount of pings using the default ICMP protocol.
2. TCP - Sends an infinite amount of pings using The TCP protocol, good to try on port 22 & 80 if ICMP pings don't reach the target.
3. Nmap scanner - Scans an IP address's open ports by using a mix of ICMP and TCP pings. Stops at port 65535.
