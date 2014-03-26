﻿namespace Stumps.Web.ViewModules
{

    using System;
    using System.Collections;
    using System.Globalization;
    using Nancy;
    using Stumps.Server;

    /// <summary>
    ///     A class that provides support the Stumps overview webpage of the Stumps website.
    /// </summary>
    public class StumpsOverviewModule : NancyModule
    {

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Stumps.Web.ViewModules.StumpsOverviewModule"/> class.
        /// </summary>
        /// <param name="stumpsHost">The <see cref="T:Stumps.Server.IStumpsHost"/> used by the instance.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="stumpsHost"/> is <c>null</c>.</exception>
        public StumpsOverviewModule(IStumpsHost stumpsHost)
        {

            if (stumpsHost == null)
            {
                throw new ArgumentNullException("stumpsHost");
            }

            Get["/proxy/{serverId}/stumps"] = _ =>
            {
                var serverId = (string)_.serverId;
                var server = stumpsHost.FindServer(serverId);

                var stumpModelArray = new ArrayList();

                var stumpContractList = server.FindAllContracts();
                foreach (var contract in stumpContractList)
                {
                    var stumpModel = new
                    {
                        StumpId = contract.StumpId,
                        StumpName = contract.StumpName
                    };

                    stumpModelArray.Add(stumpModel);
                }

                var model = new
                {
                    ProxyId = server.ServerId,
                    ExternalHostName = server.UseSsl ? server.ExternalHostName + " (SSL)" : server.ExternalHostName,
                    LocalWebsite = "http://localhost:" + server.ListeningPort.ToString(CultureInfo.InvariantCulture) + "/",
                    Stumps = stumpModelArray
                };

                return View["stumpsoverview", model];
            };

        }

    }

}