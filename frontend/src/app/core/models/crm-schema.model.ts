// CRM CSV Schema Definitions
// Source: crm-data/ folder — all files use UTF-8 (with BOM), comma delimiter.
// Pseudonymised fields: names, EANs, hashed tokens — joins across files still work.
// Dates: ISO strings ("2024-01-01"), "9999-12-31" or "99991231" = open-ended.

// ─── ArchievingSolution ───────────────────────────────────────────────────────

export interface LookupCustomerData {
  'Collective name': string;      // hashed group key — joins related parties
  'Customer number': string;
  'Debtor number': string;
  'Invoice number': string;
  'File location': string;        // full archive-drive path
}

export interface ContractPrice {
  ContractUniqueIdentifier: string;
  StartDate: string;
  EndDate: string;                // "99991231" = open-ended
  Price: number;
  Description: string;           // Dutch label, e.g. "Vaste Prijs LDN Gas G1"
}

/** Identical schema for Meter Read_1.csv through Meter Read_8.csv */
export interface MeterRead {
  EANUniqueIdentifier: string;
  StartDate: string;
  EndDate: string;
  Consumption: number;
  Position: number;
  PreviousPosition: number;
  Source: string;                 // e.g. "DVEP", "Customer"
  Quality: string;                // "Estimated" | "Customer" | "Actual"
  UsageType: string;              // "UsageLow" | "UsageHigh"
  Direction: string;              // "Consumption" | "Production"
  MeterIdentifier: string;
  MeterFactor: number;
}

export interface PriceProposition {
  ContractUniqueIdentifier: string;
  StartDate: string;
  EndDate: string;
  Price: number;
  Description: string;
}

/** Identical schema for all Timeslices-* files */
export interface Timeslice {
  EANUniqueIdentifier: string;
  StartDate: string;
  EndDate: string;
  Description: string;
}

// ─── ERPSQLServer ─────────────────────────────────────────────────────────────

export interface ConnectionType {
  ConnectionTypeId: number;
  Description: string;
  PresentationDescription: string;
  UserNameModified: string;
  TransStartDate: string;
}

export interface OrganizationType {
  OrganizationTypeId: number;
  Description: string;
  PresentationDescription: string;
  UserNameModified: string;
  TransStartDate: string;
}

export interface ProductType {
  ProductId: number;
  Description: string;
  PresentationDescription: string;
  PortaalTypeCode: string;        // e.g. "ELK"
  UserNameModified: string;
  TransStartDate: string;
}

export interface Organization {
  OrganizationId: number;
  OrganizationTypeId: number;
  DebtorReference: string | null;
  Name: string;                   // pseudonymised hash
  UserNameModified: string;
  TransStartDate: string;
}

export interface Connection {
  ConnectionId: number;
  EAN: string;                    // pseudonymised 18-digit code
  ProductType: string;            // "Electricity" | "Gas"
  DeliveryType: string;           // "LDN" | "ODN" | "NA"
  ConnectionTypeId: number;
  Description: string;
  ClientReference: string;
  ExternalReference: string;
  UserNameModified: string;
  TransStartDate: string;
}

export interface Contract {
  ContractId: number;
  ContractType: string;           // "Customer" | "Period"
  ContractReference: string;
  ProductId: number;
  StartDate: string;
  EndDate: string;
  UserComment: string;
  UserNameModified: string;
  TransStartDate: string;
  CurrentAgreedAmount: number;
}

export interface LastConnectionContact {
  connectionid: number;
  contactid: number;
  contactdate: string;
  subject: string;                // "General" | "Cancellation" | "MeasureData" | "Modification"
  report: string;                 // pseudonymised free-text hash
  Volgorde: number;               // sequence/rank
}

export interface ConnectionContact {
  // Connection columns
  ConnectionId: number;
  EAN: string;
  ProductType: string;
  DeliveryType: string;
  ConnectionTypeId: number;
  Description: string;
  ClientReference: string;
  ExternalReference: string;
  UserNameModified: string;
  TransStartDate: string;
  // Contact columns
  ConnectionContactId: number;
  ContactId: number;
  ValidStartDate: string;
  ValidEndDate: string;
  ContactDate: string;
  UserName: string;
  ContactPerson: string;
  ContactPersonType: string;      // "Person" | "PersonId"
  Subject: string;
  ProductId: number;
  Report: string;                 // pseudonymised
}

export interface OrganizationContact {
  // Organization columns
  OrganizationId: number;
  OrganizationTypeId: number;
  DebtorReference: string | null;
  Name: string;
  // Contact columns
  OrganizationContactId: number;
  ContactId: number;
  ValidStartDate: string;
  ValidEndDate: string;
  ContactDate: string;
  UserName: string;
  ContactPerson: string;
  ContactPersonType: string;
  Subject: string;
  ProductId: number;
  Report: string;
}

export interface ContractCustomerConnectionBrokerDebtor {
  EAN: string;
  ConnectionId: number;
  ContractID: number;
  ContractNumber: string;
  StartDate: string;
  EndDate: string;
  Market: string;                 // "Electricity" | "Gas"
  CustomerNumber: string;
  CustomerName: string;           // pseudonymised
  BrokerNumber: string;
  BrokerName: string;             // pseudonymised
  DebtorNumber: string;
  DebtorName: string;             // pseudonymised
}

export interface ConnectionMeterRead {
  UsageID: number;
  ConnectionId: number;
  EAN: string;
  MeterID: number;
  ReadingDate: string;
  StartDate: string;
  EndDate: string;
  MeterType: string;              // "Gas" | "Electricity"
  UsageSource: string;            // e.g. "ECH"
  UsageType: string;
  Quality: string;
  Consumption: number;
  Position: number;
  PreviousPosition: number;
  Direction: string;              // "Consumption" | "Production"
  Unit: string;                   // "m3" | "kWh"
}

export interface AnnualStandardUsage {
  ConnectionAnnualStandardUsageId: number;
  ConnectionId: number;
  EAN: string;
  ValidStartDate: string;
  ValidEndDate: string;
  EAEnergyConsumptionNettedOffPeak: number;
  EAEnergyConsumptionNettedPeak: number;
  EAEnergyProductionNettedOffPeak: number;
  EAEnergyProductionNettedPeak: number;
  AnnualStandardUsageDate: string;
}

export interface ConnectionProperty {
  ConnectionConnectionPropertyId: number;
  ConnectionId: number;
  EAN: string;
  ValidStartDate: string;
  ValidEndDate: string;
  ConnectionPropertyValue: string;
  ConnectionPropertyTypeId: number;
  Description: string;            // e.g. "StandardCost"
  PresentationDescription: string;
  ConnectionPropertyGroup: string;
}

export interface CaptarRecord {
  CaptarContractId: number;
  ConnectionId: number;
  EAN: string;
  StartDate: string;
  EndDate: string;
  ValidStartDate: string;
  ValidEndDate: string;
  NBCode: string;
  captarcode: string;
  CaptarCode: string;
  EanCaptarCode: string;
  FysicalCapicity: number;
  FysicalStatus: string;
  FysicalStatusDescription: string;
  ContractModel: string;
  VatCategoryId: number;
  ContractModelDescription: string;
  CaptarNBId: number;
  CaptarId: number;
  NBId: number;
  AmountYear: number;
  AmountDay: number;
  AmountAPYear: number;
  AmountAPMonth: number;
  AmountAPDay: number;
  AmountCaptar: number;
  AmountConnectionService: number;
  AmountMeterRent: number;
  AmountSystemService: number;
  AmountFixedCharge: number;
  VATpc: number;
  CaptarNBStartDate: string;
  CaptarNBEndDate: string;
  CaptarNBValidStartDate: string;
  CaptarNBValidEndDate: string;
}

export interface PriceComponentRecord {
  ConnectionId: number;
  EAN: string;
  ProductType: string;
  ContractId: number;
  ContractReference: string;
  Description: string;
  PresentationDescription: string;  // Dutch label
  Price: number;
  PriceComponentId: number;
  ComponentDescription: string;     // e.g. "ID_REBElectricity"
  ComponentPresentationDescription: string;
  PriceComponentPriceId: number;
  StartDate: string;
  EndDate: string;
  Type: string;                     // e.g. "Propositie"
  Name: string;                     // supplying organisation name
}

export interface MeterReadEvent {
  ConnectionId: number;
  UsageId: number;
  MeterReadingId: number;
  MeterPositionId: number;
  EAN: string;
  MeterNumber: string;              // pseudonymised
  MeterType: string;                // "Electricity" | "Gas"
  Factor: number;
  Amount: number;                   // can be negative (production)
  usg_quality: string;              // "Estimated" | "Measured" | "Customer"
  UsageType: string;                // "UsageLow" | "UsageHigh" | "kWmax"
  StartDate: string;
  EndDate: string;
  sts_description: string;
  sts_presentation_description: string;
  StatusDetails: string;
  Position: number;
  TimeFrame: string;
  pos_quality: string;
  QuantityDate: string;
  QuantityDateType: string;
  src_description: string;
  src_presentation_description: string;
}

// ─── File Registry ────────────────────────────────────────────────────────────

export interface CsvFileDescriptor<T> {
  /** Path relative to repo root */
  path: string;
  /** Column names in header order */
  columns: (keyof T)[];
  /** Whether multiple physical files share this schema */
  multiFile?: boolean;
}

export const CSV_FILES = {
  lookupCustomerData1: {
    path: 'crm-data/ArchievingSolution/[Confidential] Look-up Customer Data_1.csv',
    columns: ['Collective name', 'Customer number', 'Debtor number', 'Invoice number', 'File location'],
    multiFile: true,
  } as CsvFileDescriptor<LookupCustomerData>,

  lookupCustomerData2: {
    path: 'crm-data/ArchievingSolution/[Confidential] Look-up Customer Data_2.csv',
    columns: ['Collective name', 'Customer number', 'Debtor number', 'Invoice number', 'File location'],
    multiFile: true,
  } as CsvFileDescriptor<LookupCustomerData>,

  contractPrice: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Contract Price.csv',
    columns: ['ContractUniqueIdentifier', 'StartDate', 'EndDate', 'Price', 'Description'],
  } as CsvFileDescriptor<ContractPrice>,

  meterRead: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Meter Read_{n}.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Consumption', 'Position', 'PreviousPosition', 'Source', 'Quality', 'UsageType', 'Direction', 'MeterIdentifier', 'MeterFactor'],
    multiFile: true, // files 1–8
  } as CsvFileDescriptor<MeterRead>,

  priceProposition: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Price Proposition.csv',
    columns: ['ContractUniqueIdentifier', 'StartDate', 'EndDate', 'Price', 'Description'],
  } as CsvFileDescriptor<PriceProposition>,

  timesliceCaptarCode: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - CaptarCode.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timesliceConnectionType: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - ConnectionType.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timesliceEnergyDeliveryStatus: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - EnergyDeliveryStatus.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timeslicePhysicalStatus: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - PhysicalStatus.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timesliceProfile: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - Profile.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timesliceResidentialFunction: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - ResidentialFunction.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  timesliceUsageType: {
    path: 'crm-data/ArchievingSolution/Generic/[Confidential] Timeslices - UsageType.csv',
    columns: ['EANUniqueIdentifier', 'StartDate', 'EndDate', 'Description'],
  } as CsvFileDescriptor<Timeslice>,

  connectionTypes: {
    path: 'crm-data/ERPSQLServer/[Confidential] ConnectionTypes.csv',
    columns: ['ConnectionTypeId', 'Description', 'PresentationDescription', 'UserNameModified', 'TransStartDate'],
  } as CsvFileDescriptor<ConnectionType>,

  organizationTypes: {
    path: 'crm-data/ERPSQLServer/[Confidential] OrganizationTypes.csv',
    columns: ['OrganizationTypeId', 'Description', 'PresentationDescription', 'UserNameModified', 'TransStartDate'],
  } as CsvFileDescriptor<OrganizationType>,

  productTypes: {
    path: 'crm-data/ERPSQLServer/[Confidential] ProductTypes.csv',
    columns: ['ProductId', 'Description', 'PresentationDescription', 'PortaalTypeCode', 'UserNameModified', 'TransStartDate'],
  } as CsvFileDescriptor<ProductType>,

  organizations: {
    path: 'crm-data/ERPSQLServer/[Confidential] Organizations.csv',
    columns: ['OrganizationId', 'OrganizationTypeId', 'DebtorReference', 'Name', 'UserNameModified', 'TransStartDate'],
  } as CsvFileDescriptor<Organization>,

  connections: {
    path: 'crm-data/ERPSQLServer/[Confidential] Connections.csv',
    columns: ['ConnectionId', 'EAN', 'ProductType', 'DeliveryType', 'ConnectionTypeId', 'Description', 'ClientReference', 'ExternalReference', 'UserNameModified', 'TransStartDate'],
  } as CsvFileDescriptor<Connection>,

  contracts: {
    path: 'crm-data/ERPSQLServer/[Confidential] Contracts.csv',
    columns: ['ContractId', 'ContractType', 'ContractReference', 'ProductId', 'StartDate', 'EndDate', 'UserComment', 'UserNameModified', 'TransStartDate', 'CurrentAgreedAmount'],
  } as CsvFileDescriptor<Contract>,

  lastConnectionContacts: {
    path: 'crm-data/ERPSQLServer/[Confidential] LastConnectionContacts.csv',
    columns: ['connectionid', 'contactid', 'contactdate', 'subject', 'report', 'Volgorde'],
  } as CsvFileDescriptor<LastConnectionContact>,

  connectionContacts: {
    path: 'crm-data/ERPSQLServer/[Confidential] ConnectionContacts.csv',
    columns: ['ConnectionId', 'EAN', 'ProductType', 'DeliveryType', 'ConnectionTypeId', 'Description', 'ClientReference', 'ExternalReference', 'UserNameModified', 'TransStartDate', 'ConnectionContactId', 'ContactId', 'ValidStartDate', 'ValidEndDate', 'ContactDate', 'UserName', 'ContactPerson', 'ContactPersonType', 'Subject', 'ProductId', 'Report'],
  } as CsvFileDescriptor<ConnectionContact>,

  organizationContacts: {
    path: 'crm-data/ERPSQLServer/[Confidential] OrganizationContacts.csv',
    columns: ['OrganizationId', 'OrganizationTypeId', 'DebtorReference', 'Name', 'OrganizationContactId', 'ContactId', 'ValidStartDate', 'ValidEndDate', 'ContactDate', 'UserName', 'ContactPerson', 'ContactPersonType', 'Subject', 'ProductId', 'Report'],
  } as CsvFileDescriptor<OrganizationContact>,

  contractCustomerConnectionBrokerDebtor: {
    path: 'crm-data/ERPSQLServer/[Confidential] Contract-Customer-Connection-BrokerDebtor.csv',
    columns: ['EAN', 'ConnectionId', 'ContractID', 'ContractNumber', 'StartDate', 'EndDate', 'Market', 'CustomerNumber', 'CustomerName', 'BrokerNumber', 'BrokerName', 'DebtorNumber', 'DebtorName'],
  } as CsvFileDescriptor<ContractCustomerConnectionBrokerDebtor>,

  connectionMeterReads: {
    path: 'crm-data/ERPSQLServer/[Confidential] ConnectionMeterReads.csv',
    columns: ['UsageID', 'ConnectionId', 'EAN', 'MeterID', 'ReadingDate', 'StartDate', 'EndDate', 'MeterType', 'UsageSource', 'UsageType', 'Quality', 'Consumption', 'Position', 'PreviousPosition', 'Direction', 'Unit'],
  } as CsvFileDescriptor<ConnectionMeterRead>,

  annualStandardUsage: {
    path: 'crm-data/ERPSQLServer/[Confidential] [ValueAQuery] ASU001.csv',
    columns: ['ConnectionAnnualStandardUsageId', 'ConnectionId', 'EAN', 'ValidStartDate', 'ValidEndDate', 'EAEnergyConsumptionNettedOffPeak', 'EAEnergyConsumptionNettedPeak', 'EAEnergyProductionNettedOffPeak', 'EAEnergyProductionNettedPeak', 'AnnualStandardUsageDate'],
  } as CsvFileDescriptor<AnnualStandardUsage>,

  connectionProperties: {
    path: 'crm-data/ERPSQLServer/[Confidential] [ValueAQuery] CPY001.csv',
    columns: ['ConnectionConnectionPropertyId', 'ConnectionId', 'EAN', 'ValidStartDate', 'ValidEndDate', 'ConnectionPropertyValue', 'ConnectionPropertyTypeId', 'Description', 'PresentationDescription', 'ConnectionPropertyGroup'],
  } as CsvFileDescriptor<ConnectionProperty>,

  captars: {
    path: 'crm-data/ERPSQLServer/[Confidential] [ValueAQuery] DQE - Captars.csv',
    columns: ['CaptarContractId', 'ConnectionId', 'EAN', 'StartDate', 'EndDate', 'ValidStartDate', 'ValidEndDate', 'NBCode', 'captarcode', 'CaptarCode', 'EanCaptarCode', 'FysicalCapicity', 'FysicalStatus', 'FysicalStatusDescription', 'ContractModel', 'VatCategoryId', 'ContractModelDescription', 'CaptarNBId', 'CaptarId', 'NBId', 'AmountYear', 'AmountDay', 'AmountAPYear', 'AmountAPMonth', 'AmountAPDay', 'AmountCaptar', 'AmountConnectionService', 'AmountMeterRent', 'AmountSystemService', 'AmountFixedCharge', 'VATpc', 'CaptarNBStartDate', 'CaptarNBEndDate', 'CaptarNBValidStartDate', 'CaptarNBValidEndDate'],
  } as CsvFileDescriptor<CaptarRecord>,

  priceComponents: {
    path: 'crm-data/ERPSQLServer/[Confidential] [ValueAQuery] DQE - Prijzen v5 met Organization.csv',
    columns: ['ConnectionId', 'EAN', 'ProductType', 'ContractId', 'ContractReference', 'Description', 'PresentationDescription', 'Price', 'PriceComponentId', 'ComponentDescription', 'ComponentPresentationDescription', 'PriceComponentPriceId', 'StartDate', 'EndDate', 'Type', 'Name'],
  } as CsvFileDescriptor<PriceComponentRecord>,

  meterReadEvents: {
    path: 'crm-data/ERPSQLServer/[Confidential] [ValueAQuery] ERPMRE.csv',
    columns: ['ConnectionId', 'UsageId', 'MeterReadingId', 'MeterPositionId', 'EAN', 'MeterNumber', 'MeterType', 'Factor', 'Amount', 'usg_quality', 'UsageType', 'StartDate', 'EndDate', 'sts_description', 'sts_presentation_description', 'StatusDetails', 'Position', 'TimeFrame', 'pos_quality', 'QuantityDate', 'QuantityDateType', 'src_description', 'src_presentation_description'],
  } as CsvFileDescriptor<MeterReadEvent>,
} as const;
