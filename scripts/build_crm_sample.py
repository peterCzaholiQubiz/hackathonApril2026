from __future__ import annotations

import argparse
import csv
import json
import shutil
from collections import Counter, defaultdict
from pathlib import Path

csv.field_size_limit(1024 * 1024 * 1024)


FILES_TO_FILTER = [
    "Organizations.csv",
    "Contract-Customer-Connection-BrokerDebtor.csv",
    "Contracts.csv",
    "Connections.csv",
    "ConnectionMeterReads.csv",
    "OrganizationContacts.csv",
    "ConnectionContacts.csv",
    "Look-up Customer Data_1.csv",
    "Look-up Customer Data_2.csv",
    "Meter Read_1.csv",
    "Meter Read_2.csv",
    "Meter Read_3.csv",
    "Meter Read_4.csv",
    "Meter Read_5.csv",
    "Meter Read_6.csv",
    "Meter Read_7.csv",
    "Meter Read_8.csv",
]


def normalize(value: str | None) -> str:
    if value is None:
        return ""

    trimmed = value.strip()
    return "" if trimmed.upper() == "NULL" else trimmed


def build_index(header: list[str]) -> dict[str, int]:
    return {name: idx for idx, name in enumerate(header)}


def value(row: list[str], header_index: dict[str, int], column: str) -> str:
    index = header_index.get(column)
    if index is None or index >= len(row):
        return ""

    return normalize(row[index])


def iter_csv_rows(path: Path):
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.reader(handle)
        header = next(reader)
        header_index = build_index(header)
        yield header, header_index, None

        for row in reader:
            if not row:
                continue

            yield header, header_index, row


def count_by_key(path: Path, column: str) -> Counter[str]:
    counts: Counter[str] = Counter()

    for _, header_index, row in iter_csv_rows(path):
        if row is None:
            continue

        key = value(row, header_index, column)
        if key:
            counts[key] += 1

    return counts


def load_customers(path: Path) -> dict[str, dict]:
    customers: dict[str, dict] = {}
    rank = 0

    for header, header_index, row in iter_csv_rows(path):
        if row is None:
            continue

        if value(row, header_index, "OrganizationTypeId") != "2":
            continue

        org_id = value(row, header_index, "OrganizationId")
        debtor_reference = value(row, header_index, "DebtorReference")
        name = value(row, header_index, "Name")

        if not org_id or not debtor_reference:
            continue

        if org_id in customers:
            continue

        customers[org_id] = {
            "row": row,
            "debtor_reference": debtor_reference,
            "name": name,
            "rank": rank,
        }
        rank += 1

    return customers


def load_join_maps(path: Path) -> tuple[dict[str, set[str]], dict[str, set[str]]]:
    contracts_by_customer: dict[str, set[str]] = defaultdict(set)
    connections_by_customer: dict[str, set[str]] = defaultdict(set)

    for _, header_index, row in iter_csv_rows(path):
        if row is None:
            continue

        customer_number = value(row, header_index, "CustomerNumber")
        if not customer_number:
            continue

        contract_id = value(row, header_index, "ContractID")
        connection_id = value(row, header_index, "ConnectionId")

        if contract_id:
            contracts_by_customer[customer_number].add(contract_id)

        if connection_id:
            connections_by_customer[customer_number].add(connection_id)

    return contracts_by_customer, connections_by_customer


def load_connection_eans(path: Path) -> dict[str, str]:
    connection_eans: dict[str, str] = {}

    for _, header_index, row in iter_csv_rows(path):
        if row is None:
            continue

        connection_id = value(row, header_index, "ConnectionId")
        if not connection_id:
            continue

        connection_eans[connection_id] = value(row, header_index, "EAN")

    return connection_eans


def filter_csv(path_in: Path, path_out: Path, keep_row) -> int:
    kept = 0

    with path_in.open("r", encoding="utf-8-sig", newline="") as source, path_out.open(
        "w", encoding="utf-8", newline=""
    ) as target:
        reader = csv.reader(source)
        writer = csv.writer(target, lineterminator="\n")

        header = next(reader)
        header_index = build_index(header)
        writer.writerow(header)

        for row in reader:
            if not row:
                continue

            if keep_row(row, header_index):
                writer.writerow(row)
                kept += 1

    return kept


def main() -> None:
    repo_root = Path(__file__).resolve().parent.parent
    crm_data_root = repo_root / "crm-data"

    parser = argparse.ArgumentParser(description="Build a reduced flat CRM sample dataset.")
    parser.add_argument(
        "--source",
        type=Path,
        default=crm_data_root / "All",
        help="Flat source CRM folder that contains the importer-used CSVs.",
    )
    parser.add_argument(
        "--customer-count",
        type=int,
        default=100,
        help="Number of customers to keep in the sample dataset.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Output folder for the reduced sample. Defaults to crm-data/Sample-<customer-count>.",
    )
    args = parser.parse_args()

    source_root = args.source.resolve()
    output_root = (args.output or (crm_data_root / f"Sample-{args.customer_count}")).resolve()

    if source_root == output_root:
        raise ValueError("The output folder must be different from the source folder.")

    if not source_root.exists():
        raise FileNotFoundError(f"Source folder does not exist: {source_root}")

    if output_root.parent != crm_data_root.resolve():
        raise ValueError(f"Output must be a direct child folder of {crm_data_root.resolve()}.")

    if output_root == crm_data_root.resolve() or source_root == crm_data_root.resolve():
        raise ValueError("Refusing to use the crm-data root as an input or output dataset folder.")

    organizations_path = source_root / "[Confidential] Organizations.csv"
    join_path = source_root / "[Confidential] Contract-Customer-Connection-BrokerDebtor.csv"
    contracts_path = source_root / "[Confidential] Contracts.csv"
    connections_path = source_root / "[Confidential] Connections.csv"
    connection_meter_reads_path = source_root / "[Confidential] ConnectionMeterReads.csv"
    organization_contacts_path = source_root / "[Confidential] OrganizationContacts.csv"
    connection_contacts_path = source_root / "[Confidential] ConnectionContacts.csv"
    invoice_paths = [
        source_root / "[Confidential] Look-up Customer Data_1.csv",
        source_root / "[Confidential] Look-up Customer Data_2.csv",
    ]

    customers = load_customers(organizations_path)
    contracts_by_customer, connections_by_customer = load_join_maps(join_path)
    connection_eans = load_connection_eans(connections_path)
    invoice_counts: Counter[str] = Counter()

    for invoice_path in invoice_paths:
        invoice_counts.update(count_by_key(invoice_path, "Customer number"))

    organization_contact_counts = count_by_key(organization_contacts_path, "OrganizationId")
    connection_contact_counts = count_by_key(connection_contacts_path, "ConnectionId")
    connection_meter_read_counts = count_by_key(connection_meter_reads_path, "ConnectionId")

    ranked_customers: list[tuple[tuple, str, dict]] = []
    for org_id, customer in customers.items():
        debtor_reference = customer["debtor_reference"]
        connection_ids = connections_by_customer.get(debtor_reference, set())
        contract_ids = contracts_by_customer.get(debtor_reference, set())
        ean_count = sum(1 for connection_id in connection_ids if connection_eans.get(connection_id))
        connection_interactions = sum(connection_contact_counts[connection_id] for connection_id in connection_ids)
        connection_meter_reads = sum(connection_meter_read_counts[connection_id] for connection_id in connection_ids)
        invoices = invoice_counts[debtor_reference]
        organization_interactions = organization_contact_counts[org_id]

        score = (
            1 if invoices > 0 else 0,
            1 if (organization_interactions + connection_interactions) > 0 else 0,
            1 if connection_meter_reads > 0 else 0,
            1 if ean_count > 0 else 0,
            connection_meter_reads,
            invoices,
            organization_interactions + connection_interactions,
            len(connection_ids),
            len(contract_ids),
            -customer["rank"],
        )

        ranked_customers.append((score, org_id, customer))

    ranked_customers.sort(reverse=True)
    selected = ranked_customers[: args.customer_count]

    if len(selected) < args.customer_count:
        raise ValueError(f"Requested {args.customer_count} customers but found only {len(selected)} eligible customers.")

    selected_org_ids = {org_id for _, org_id, _ in selected}
    selected_customer_numbers = {customer["debtor_reference"] for _, _, customer in selected}
    selected_contract_ids: set[str] = set()
    selected_connection_ids: set[str] = set()

    for customer_number in selected_customer_numbers:
        selected_contract_ids.update(contracts_by_customer.get(customer_number, set()))
        selected_connection_ids.update(connections_by_customer.get(customer_number, set()))

    if output_root.exists():
        shutil.rmtree(output_root)

    output_root.mkdir(parents=True)

    counts: dict[str, int] = {}

    counts["Organizations.csv"] = filter_csv(
        organizations_path,
        output_root / "[Confidential] Organizations.csv",
        lambda row, idx: value(row, idx, "OrganizationId") in selected_org_ids
        and value(row, idx, "OrganizationTypeId") == "2",
    )

    counts["Contract-Customer-Connection-BrokerDebtor.csv"] = filter_csv(
        join_path,
        output_root / "[Confidential] Contract-Customer-Connection-BrokerDebtor.csv",
        lambda row, idx: value(row, idx, "CustomerNumber") in selected_customer_numbers,
    )

    selected_eans: set[str] = set()

    def keep_connection(row: list[str], idx: dict[str, int]) -> bool:
        connection_id = value(row, idx, "ConnectionId")
        if connection_id not in selected_connection_ids:
            return False

        ean = value(row, idx, "EAN")
        if ean:
            selected_eans.add(ean)

        return True

    counts["Contracts.csv"] = filter_csv(
        contracts_path,
        output_root / "[Confidential] Contracts.csv",
        lambda row, idx: value(row, idx, "ContractId") in selected_contract_ids,
    )

    counts["Connections.csv"] = filter_csv(
        connections_path,
        output_root / "[Confidential] Connections.csv",
        keep_connection,
    )

    counts["ConnectionMeterReads.csv"] = filter_csv(
        connection_meter_reads_path,
        output_root / "[Confidential] ConnectionMeterReads.csv",
        lambda row, idx: value(row, idx, "ConnectionId") in selected_connection_ids,
    )

    counts["OrganizationContacts.csv"] = filter_csv(
        organization_contacts_path,
        output_root / "[Confidential] OrganizationContacts.csv",
        lambda row, idx: value(row, idx, "OrganizationId") in selected_org_ids,
    )

    counts["ConnectionContacts.csv"] = filter_csv(
        connection_contacts_path,
        output_root / "[Confidential] ConnectionContacts.csv",
        lambda row, idx: value(row, idx, "ConnectionId") in selected_connection_ids,
    )

    for invoice_path in invoice_paths:
        counts[invoice_path.name.replace("[Confidential] ", "")] = filter_csv(
            invoice_path,
            output_root / invoice_path.name,
            lambda row, idx: value(row, idx, "Customer number") in selected_customer_numbers,
        )

    for meter_read_number in range(1, 9):
        file_name = f"[Confidential] Meter Read_{meter_read_number}.csv"
        meter_read_path = source_root / file_name
        counts[file_name.replace("[Confidential] ", "")] = filter_csv(
            meter_read_path,
            output_root / file_name,
            lambda row, idx: value(row, idx, "EANUniqueIdentifier") in selected_eans,
        )

    summary = {
        "sourceRoot": str(source_root.relative_to(repo_root).as_posix()),
        "outputRoot": str(output_root.relative_to(repo_root).as_posix()),
        "customerCount": len(selected),
        "selectedOrganizations": [
            {
                "organizationId": org_id,
                "debtorReference": customer["debtor_reference"],
                "name": customer["name"],
            }
            for _, org_id, customer in selected
        ],
        "rowsByFile": counts,
    }

    summary_path = output_root / "sample-summary.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")

    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
