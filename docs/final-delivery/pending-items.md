# Outstanding Items & Operational Readiness Roadmap

## 1. Production Go-Live Checklist

| Item ID | Task Description | Target Environment | Priority | Target Timeline |
| :--- | :--- | :--- | :--- | :--- |
| **ACT-01** | Execute distributed k6 load testing against live Azure AKS cluster using Scenario A, B, and C scripts. | Staging / Pre-Prod | **High** | Pre-Go-Live |
| **ACT-02** | Migrate Cosmos DB `claims` container to Hierarchical Partition Keys `["/tenantId", "/serviceDate"]`. | Prod Infrastructure | **High** | Pre-Go-Live |
| **ACT-03** | Finalize legal Business Associate Agreements (BAAs) with all 50+ payer organizations and EDI clearinghouses. | Legal / Compliance | **Critical** | Pre-Contract |
| **ACT-04** | Provision Microsoft Defender for Cloud and Microsoft Sentinel for automated SIEM/SOAR incident detection. | Azure Cloud Subscription | **High** | Pre-Go-Live |
| **ACT-05** | Deploy full Claims Adjudication Engine, Provider Directory, and Notification microservices beyond POC scope. | Kubernetes Cluster | **Medium** | Phase 2 Roadmap |
| **ACT-06** | Configure Azure Cosmos DB multi-region active-passive failover and Azure SQL active geo-replication. | Disaster Recovery (DR) | **High** | Pre-Go-Live |
| **ACT-07** | Implement Azure Front Door / Traffic Manager with Web Application Firewall (WAF) OWASP Top 10 rule sets. | Network Perimeter | **High** | Pre-Go-Live |
| **ACT-08** | Implement Power BI Embedded application with Azure AD service principal and automated RLS session filtering. | Reporting Tier | **Medium** | Post-Go-Live |

---

## 2. Infrastructure Optimization Roadmap

1. **FinOps Cost Optimization:** Transition non-production nodepools to Azure Spot instances for batch testing and implement Azure Reservation (1-year or 3-year RI) for baseline AKS nodes and Cosmos DB autoscale throughput.
2. **KEDA Scheduled Scaling:** Pre-scale the Claims Ingestion pod deployment to 8 replicas at 07:45 AM EST on business days to absorb anticipated morning clearinghouse batch spikes without cold start.
3. **Automated Secret Rotation Auditing:** Configure an Azure Event Grid rule on Key Vault secret expiration events to trigger an Azure Function verifying that AKS pods have dynamically re-read updated credentials.
