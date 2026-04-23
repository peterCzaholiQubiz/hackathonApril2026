# Customer Portfolio Thermometer

## Description
Energy companies store vast amounts of customer data in CRM (Customer Relationship Management) systems, including contracts, interaction history, billing status, complaints, payment behavior, and service notes. However, this data is rarely translated into structured risk and opportunity intelligence. The challenge is to build a Customer Portfolio Thermometer: a smart, non intrusive analytics layer that connects in read-only mode to a CRM system and generates a dynamic portfolio overview.

## Business Context / Why This Matters?
The energy sector is entering a phase where customer risk and engagement dynamics are becoming more important than static customer profiles. Traditional CRM systems and billing platforms store vast amounts of administrative, contractual, and interaction data, but they do not translate this information into structured portfolio intelligence. They record what happened, but they do not explain which customers are becoming risky, which segments require attention, or where hidden value can be unlocked. For suppliers, BRPs (Balance Responsible Parties), LPG distributors, and energy service providers, the real challenge is not the lack of data, but the lack of insight. Early signals of churn, payment risk, dissatisfaction, operational stress, or declining engagement are often buried in fragmented CRM records, communication logs, and support notes. Without an intelligent layer above CRM, organizations remain reactive instead of proactive.

## Specific Requirements
Read-only integration with an existing CRM dataset, no data mutation
Ability to process structured CRM data and optionally unstructured communication such as emails or call notes
Demonstrable PoC that runs end-to-end and can be shown live on a laptop or tablet
NOTE: For this challenge, teams are allowed to create or enrich data themselves if this strengthens the clarity and impact of the demonstration.

## Data Provided
A copy of the DVEP CRM and metering database is available

## Solution (Expected Outcome)
The demonstration should show:

- A portfolio heat overview, for example green, yellow, red segments
- Identified risk groups such as churn risk, payment risk, imbalance risk, service dissatisfaction, low predictability
- Clear explanation per risk group why it is flagged
- Suggested actions such as proactive outreach, contract adjustment, advisory offer, monitoring


## Q&A Knowledge Base
- “What are potential pitfalls in flagging customers as ‘red’ or ‘high risk’, and how can explainability and transparency mitigate those concerns?”
    - Hi. Great question and this is one of the sensitive areas. When you flag a customer as a high risk and this is incorrect, you loose a customer. So make sure that you advise instead of tell. And be transparant in the reasoning. A good example is what currently happens in the NL. There are energy companies giving free electricity during the weekend 12.00 - 17.00. But not when you use it for batteries (so discharge free electricity during the other moments). I am curious how they determine somebody charged a battery and not eg an electric car.

- “How can we keep the ‘thermometer’ non‑intrusive for existing CRM/billing systems, while still giving a powerful portfolio heatmap for business users?”
    - Hi. Great question and this is one of the elements important during the Hackathon. The biggest concern of an IT manager is 'don't introduce risks to my infrastructure'.
    - So keep it non-intrusive by avoiding changes to core systems and instead buildi a lightweight layer on top that reads data, process it externally, and visualize results in a separate interface

- “If you had to implement a first version of the ‘Customer Portfolio Thermometer’, which three risk dimensions would you start with (e.g., churn, payment, imbalance, complaints) and why? ​”
    - Hi. I would say the following three:

        1. Churn Better to keep a customer than find a new one. Price is less of a reason to leave. Its trust, service delivered...

        2. Payment Especially with the current conditions, paying the invoice can be a problem. Getting the money is costly. At the end, when you outsource deb collections, you normally get 10% of the invoice value

        3. Margin Behavior is different than contracted. Margin could become under pressure

    - Dimensions like imbalance are more suitable for the Consumption Twin challenge