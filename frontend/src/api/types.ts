// Types miroirs des DTO de l'API.

export type ProductCategory = 'Bike' | 'Component' | 'Accessory' | 'Consumable'

export interface Product {
  id: string
  sku: string
  name: string
  category: ProductCategory
  unitPrice: number
  unitCost: number
  vatRate: number
  quantityOnHand: number
  reorderThreshold: number
  preferredSupplierId: string | null
}

export interface StockMovement {
  id: string
  productId: string
  quantity: number
  reason: string
  reference: string | null
  occurredAt: string
}

export interface Mechanic {
  id: string
  name: string
}

export type RepairJobStatus = 'Scheduled' | 'InProgress' | 'Completed' | 'Invoiced' | 'Cancelled'

export interface RepairJobPart {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  vatRate: number
}

export interface RepairJob {
  id: string
  customerName: string
  customerPhone: string | null
  bikeDescription: string
  mechanicId: string
  start: string
  end: string
  status: RepairJobStatus
  notes: string | null
  laborHours: number
  laborRate: number
  parts: RepairJobPart[]
  invoiceId: string | null
}

export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Cancelled'

export interface InvoiceLine {
  description: string
  quantity: number
  unitPrice: number
  vatRate: number
  isService: boolean
}

export interface Invoice {
  id: string
  number: string
  customerName: string
  issueDate: string
  dueDate: string
  status: InvoiceStatus
  sourceType: string | null
  sourceId: string | null
  lines: InvoiceLine[]
  totalExclVat: number
  totalVat: number
  totalInclVat: number
}

export interface RegisterSession {
  id: string
  openedAt: string
  closedAt: string | null
  openingFloat: number
  countedCash: number | null
  status: 'Open' | 'Closed'
}

export interface SessionSummary {
  id: string
  openedAt: string
  closedAt: string | null
  status: string
  openingFloat: number
  saleCount: number
  cashTotal: number
  cardTotal: number
  total: number
  expectedCash: number
  countedCash: number | null
  cashDifference: number | null
}

export interface SaleLine {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  vatRate: number
}

export interface Sale {
  id: string
  sessionId: string
  number: string
  at: string
  paymentMethod: 'Cash' | 'Card'
  lines: SaleLine[]
  totalExclVat: number
  totalVat: number
  totalInclVat: number
}

export interface Supplier {
  id: string
  name: string
  email: string | null
  phone: string | null
  leadTimeDays: number
}

export type PurchaseOrderStatus = 'Draft' | 'Ordered' | 'Received' | 'Cancelled'

export interface PurchaseOrderLine {
  productId: string
  productName: string
  quantity: number
  unitCost: number
  vatRate: number
}

export interface PurchaseOrder {
  id: string
  number: string
  supplierId: string
  status: PurchaseOrderStatus
  createdAt: string
  receivedAt: string | null
  lines: PurchaseOrderLine[]
  totalExclVat: number
  totalVat: number
  totalInclVat: number
}

export interface ReplenishmentSuggestion {
  id: string
  sku: string
  name: string
  quantityOnHand: number
  reorderThreshold: number
  suggestedQuantity: number
  preferredSupplierId: string | null
  preferredSupplierName: string | null
}

export interface Account {
  number: string
  name: string
  type: string
  debit: number
  credit: number
  balance: number
}

export interface JournalLine {
  account: string
  debit: number
  credit: number
}

export interface JournalEntry {
  entryId: string
  date: string
  label: string
  reference: string | null
  lines: JournalLine[]
}

export interface LedgerLine {
  date: string
  label: string
  reference: string | null
  debit: number
  credit: number
  runningBalance: number
}
