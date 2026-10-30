/* Bản sao của wwwroot/js/online-auth-errors.js (game) dùng cho thẻ Tài khoản của Launcher; sửa thì sửa cả hai. */
/* Supabase Auth trả mã lỗi tiếng Anh (error_code); đổi sang câu tiếng Việt cho người chơi. */
const AUTH_ERRORS={
  invalid_credentials:'Email hoặc mật khẩu không đúng.',
  email_not_confirmed:'Email chưa được xác nhận. Hãy mở thư xác nhận hoặc bấm "Gửi lại email xác nhận".',
  user_already_exists:'Email này đã có tài khoản. Hãy đăng nhập.',
  email_exists:'Email này đã có tài khoản. Hãy đăng nhập.',
  weak_password:'Mật khẩu quá yếu. Hãy dùng ít nhất 8 ký tự, có cả chữ và số.',
  same_password:'Mật khẩu mới phải khác mật khẩu cũ.',
  email_address_invalid:'Địa chỉ email không hợp lệ.',
  signup_disabled:'Hiện chưa mở đăng ký tài khoản mới.',
  over_email_send_rate_limit:'Gửi email quá nhanh. Hãy chờ vài phút rồi thử lại.',
  over_request_rate_limit:'Thao tác quá nhanh. Hãy thử lại sau.',
  session_not_found:'Phiên đã hết hạn. Hãy đăng nhập lại.',
  bad_jwt:'Liên kết đã hết hạn hoặc không hợp lệ. Hãy yêu cầu liên kết mới.',
  otp_expired:'Liên kết đã hết hạn hoặc đã được dùng. Hãy yêu cầu liên kết mới.'
};
function authError(result,fallback){
  return AUTH_ERRORS[result?.error_code]||result?.error||result?.error_description||result?.msg||result?.message||fallback;
}
