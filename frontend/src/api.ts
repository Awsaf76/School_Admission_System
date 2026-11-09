import axios from 'axios'
export const api = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080', timeout: 10000 })
export type SchoolClass = { id:string; name:string; totalSeats:number; heldSeats:number; confirmedSeats:number; availableSeats:number }
export type Hold = { holdId:string; classId:string; className?:string; status:'HELD'|'CONFIRMED'|'EXPIRED'|'CANCELLED'; expiresAt:string }
