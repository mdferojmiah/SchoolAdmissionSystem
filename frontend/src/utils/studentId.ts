export const STUDENT_ID_LENGTH = 17;
export const STUDENT_ID_PATTERN = /^\d{17}$/;

export function isValidStudentId(value: string): boolean {
  return STUDENT_ID_PATTERN.test(value);
}
