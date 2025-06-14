﻿using eTickets.Data;
using eTickets.Data.Static;
using eTickets.Data.ViewModels;
using eTickets.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eTickets.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AppDbContext context,
            IEmailSender emailSender,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
            _env = env;
        }

        // GET: /Account/Register
        public IActionResult Register() => View(new RegisterVM());

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(RegisterVM registerVM)
        {
            if (!ModelState.IsValid)
                return View(registerVM);

            if (await _userManager.FindByEmailAsync(registerVM.EmailAddress) != null)
            {
                TempData["Error"] = "This email address is already in use.";
                return View(registerVM);
            }

            var newUser = new ApplicationUser
            {
                FullName = registerVM.FullName,
                Email = registerVM.EmailAddress,
                UserName = registerVM.EmailAddress,
                EmailConfirmed = false,
                IsApproved = !registerVM.ApplyAsPartner,
                Role = registerVM.ApplyAsPartner ? "Partner" : "Learner"
            };

            var createResult = await _userManager.CreateAsync(newUser, registerVM.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(registerVM);
            }

            // Upload partner file
            if (registerVM.ApplyAsPartner && registerVM.PartnerDocument != null)
            {
                var allowedTypes = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                var ext = Path.GetExtension(registerVM.PartnerDocument.FileName).ToLower();
                if (!allowedTypes.Contains(ext) || registerVM.PartnerDocument.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("PartnerDocument", "Invalid file. Only PDF/JPG/PNG under 10MB allowed.");
                    return View(registerVM);
                }

                string fileName = $"{Guid.NewGuid()}{ext}";
                string uploadPath = Path.Combine(_env.WebRootPath, "uploads", "partner_docs");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                using var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create);
                await registerVM.PartnerDocument.CopyToAsync(stream);

                newUser.PartnerDocumentPath = $"/uploads/partner_docs/{fileName}";
                await _userManager.UpdateAsync(newUser);
            }

            // Gán đúng role đã chọn
            var roleResult = await _userManager.AddToRoleAsync(newUser, newUser.Role);
            if (!roleResult.Succeeded)
            {
                TempData["Error"] = "User created, but failed to assign role.";
                return View(registerVM);
            }

            // Tạo token xác thực email
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(newUser);
            var confirmationLink = Url.Action(nameof(ConfirmEmail), "Account", new { userId = newUser.Id, token }, Request.Scheme);

            await _emailSender.SendEmailAsync(newUser.Email,
                "Confirm your email",
                $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

            return View("RegisterCompleted");
        }


        // Forgot Password
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                TempData["Error"] = "Email không hợp lệ hoặc chưa được xác nhận.";
                return View();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new { token, email = model.Email }, Request.Scheme);

            await _emailSender.SendEmailAsync(model.Email, "Reset your password", $"Click here to reset your password: <a href='{resetLink}'>Reset</a>");

            return View("ForgotPasswordConfirmation");
        }
        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            return View(new ResetPasswordVM { Token = token, Email = email });
        }
        // Reset Password
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["Error"] = "Email không tồn tại";
                return View();
            }

            var resetPassResult = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (!resetPassResult.Succeeded)
            {
                foreach (var error in resetPassResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }

            return RedirectToAction("Login", "Account");
        }

        // GOOGLE LOGIN
        [HttpGet]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                TempData["Error"] = $"Error from external provider: {remoteError}";
                return RedirectToAction("Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction("Login");

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false, true);

            if (signInResult.Succeeded)
                return LocalRedirect(returnUrl);

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        Email = email,
                        UserName = email,
                        EmailConfirmed = true
                    };
                    await _userManager.CreateAsync(user);
                    await _userManager.AddLoginAsync(user, info);
                }

                await _signInManager.SignInAsync(user, false);
                return LocalRedirect(returnUrl);
            }

            TempData["Error"] = "Email claim not received.";
            return RedirectToAction("Login");
        }



        // GET: /Account/Login
        public async Task<IActionResult> Login(ApplicationUser user)
        {
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError("", "You must confirm your email to log in.");
                
            }
            return View(new LoginVM());
        }
        // Xác nhận email
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return RedirectToAction("Index", "Home");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return View("Error");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
                return View("ConfirmEmail");

            return View("Error");
        }

        // POST: /Account/Login
        [HttpPost]

        public async Task<IActionResult> Login(LoginVM loginVM)
        {
            if (!ModelState.IsValid)
                return View(loginVM);

            var user = await _userManager.FindByEmailAsync(loginVM.EmailAddress);
            if (user == null)
            {
                TempData["Error"] = "Wrong credentials. Please, try again!";
                return View(loginVM);
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, loginVM.Password);
            if (!passwordValid)
            {
                TempData["Error"] = "Wrong credentials. Please, try again!";
                return View(loginVM);
            }

            var signInResult = await _signInManager.PasswordSignInAsync(
                user, loginVM.Password, isPersistent: false, lockoutOnFailure: false);

            if (!signInResult.Succeeded)
            {
                TempData["Error"] = "Wrong credentials. Please, try again!";
                return View(loginVM);
            }

            // Đăng nhập thành công → chuyển đến Movies/Index
            return RedirectToAction("Index", "Movies");
        }

       


        // POST: /Account/Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Movies");
        }

        // Xem danh sách users (ví dụ admin)
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        // Khi truy cập denied
        public IActionResult AccessDenied(string returnUrl) => View();
    }
}
