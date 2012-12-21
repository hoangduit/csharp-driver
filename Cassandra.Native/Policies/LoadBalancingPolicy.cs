﻿using System;
using System.Collections.Generic;
using System.Text;
using Cassandra.Native;

namespace Cassandra
{
    /**
     * The policy that decides which Cassandra hosts to contact for each new query.
     * <p>
     * Two methods need to be implemented:
     * <ul>
     *   <li>{@link LoadBalancingPolicy#distance}: returns the "distance" of an
     *   host for that balancing policy. </li>
     *   <li>{@link LoadBalancingPolicy#newQueryPlan}: it is used for each query to
     *   find which host to query first, and which hosts to use as failover.</li>
     * </ul>
     * <p>
     * The {@code LoadBalancingPolicy} is a {@link com.datastax.driver.core.Host.StateListener}
     * and is thus informed of hosts up/down events. For efficiency purposes, the
     * policy is expected to exclude down hosts from query plans.
     */
    public interface LoadBalancingPolicy
    {
        /**
          * Initialize this load balancing policy.
          * <p>
          * Note that the driver guarantees that it will call this method exactly
          * once per policy object and will do so before any call to another of the
          * methods of the policy.
          *
          * @param cluster the {@code Cluster} instance for which the policy is created.
          * @param hosts the initial hosts to use.
          */
        void Initialize(ICassandraSessionInfoProvider infoProvider);

        /**
         * Returns the distance assigned by this policy to the provided host.
         * <p>
         * The distance of an host influence how much connections are kept to the
         * node (see {@link HostDistance}). A policy should assign a {@code
         * LOCAL} distance to nodes that are susceptible to be returned first by
         * {@code newQueryPlan} and it is useless for {@code newQueryPlan} to
         * return hosts to which it assigns an {@code IGNORED} distance.
         * <p>
         * The host distance is primarily used to prevent keeping too many
         * connections to host in remote datacenters when the policy itself always
         * picks host in the local datacenter first.
         *
         * @param host the host of which to return the distance of.
         * @return the HostDistance to {@code host}.
         */
        CassandraHostDistance Distance(CassandraClusterHost host);

        /**
         * Returns the hosts to use for a new query.
         * <p>
         * Each new query will call this method. The first host in the result will
         * then be used to perform the query. In the event of a connection problem
         * (the queried host is down or appear to be so), the next host will be
         * used. If all hosts of the returned {@code Iterator} are down, the query
         * will fail.
         *
         * @param query the query for which to build a plan.
         * @return an iterator of Host. The query is tried against the hosts
         * returned by this iterator in order, until the query has been sent
         * successfully to one of the host.
         */
        IEnumerable<CassandraClusterHost> NewQueryPlan(CassandraRoutingKey routingKey);
    }
}