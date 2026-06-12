export const money = (value: number) =>
  value.toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' })

export const dateTime = (iso: string) =>
  new Date(iso).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' })

export const date = (iso: string) => new Date(iso).toLocaleDateString('fr-FR')

export const time = (iso: string) =>
  new Date(iso).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })
