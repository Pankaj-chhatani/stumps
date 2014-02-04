﻿namespace Stumps.Utility {

    using System;
    using System.Net;
    using System.Net.NetworkInformation;

    internal static class NetworkUtility {

        public const int MinimumPort = 7000;
        public const int MaximumPort = 10000;

        public static int FindRandomOpenPort() {

            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var endpointList = properties.GetActiveTcpConnections();

            var usedPorts = new bool[NetworkUtility.MaximumPort - NetworkUtility.MinimumPort + 1];

            foreach ( var endpoint in endpointList ) {
                if ( endpoint.LocalEndPoint.Port >= NetworkUtility.MinimumPort && endpoint.LocalEndPoint.Port <= NetworkUtility.MaximumPort ) {
                    var port = endpoint.LocalEndPoint.Port - NetworkUtility.MinimumPort;
                    usedPorts[port] = true;
                }
            }

            var rnd = new Random();

            var foundPort = -1;

            // Maximum 100 tries for sanity
            for ( int i = 0; i < 100; i++ ) {
                var portGuess = rnd.Next(usedPorts.Length - 1);

                if ( !usedPorts[portGuess] ) {
                    foundPort = portGuess + NetworkUtility.MinimumPort;
                    break;
                }
            }

            return foundPort;

        }

        public static bool IsPortBeingUsed(int localPort) {

            if ( localPort < IPEndPoint.MinPort || localPort > IPEndPoint.MaxPort ) {
                return true;
            }


            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endpointList = properties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in endpointList)
            {
                if (endpoint.Port == localPort)
                {
                    return true;
                }
            }
            return false;

        }

    }

}
