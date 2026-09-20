// 正道：受け入れ基準を具体的な入力・期待結果・例外条件に分解してから書く。
// テストメソッド名は「何を入れて何が起きるか」を表す。
// アサーションは結果の中身（例外の型・フィールド・メッセージキー）を見る。

using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Example.User
{
    public class UserRegistrationValidator
    {
        private static readonly Regex EmailPattern =
                new Regex(@"^[A-Za-z0-9+_.-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$",
                        RegexOptions.Compiled);

        public void Validate(UserRegistrationInput input)
        {
            if (input == null)
            {
                throw new ValidationException("input", "must_not_be_null");
            }
            ValidateEmail(input.Email);
            ValidatePassword(input.Password);
            ValidateAge(input.Age);
        }

        private void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ValidationException("email", "must_not_be_blank");
            }
            if (!EmailPattern.IsMatch(email))
            {
                throw new ValidationException("email", "invalid_format");
            }
        }

        private void ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ValidationException("password", "must_not_be_blank");
            }
            if (password.Length < 8)
            {
                throw new ValidationException("password", "too_short");
            }
            bool hasUpper = false, hasLower = false, hasDigit = false;
            foreach (char c in password)
            {
                hasUpper |= char.IsUpper(c);
                hasLower |= char.IsLower(c);
                hasDigit |= char.IsDigit(c);
            }
            if (!(hasUpper && hasLower && hasDigit))
            {
                throw new ValidationException("password", "missing_character_class");
            }
        }

        private void ValidateAge(int age)
        {
            if (age < 18)
            {
                throw new ValidationException("age", "below_minimum");
            }
            if (age > 120)
            {
                throw new ValidationException("age", "above_maximum");
            }
        }
    }

    public class UserRegistrationInput
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public int Age { get; set; }
    }

    public class ValidationException : Exception
    {
        public string Field { get; private set; }
        public string Code { get; private set; }

        public ValidationException(string field, string code)
            : base(field + ":" + code)
        {
            Field = field;
            Code = code;
        }
    }

    [TestClass]
    public class UserRegistrationValidatorTest
    {
        private readonly UserRegistrationValidator validator = new UserRegistrationValidator();

        private static UserRegistrationInput Input(
                string email, string password, int age)
        {
            return new UserRegistrationInput
            {
                Email = email,
                Password = password,
                Age = age
            };
        }

        [TestMethod]
        public void ValidInput_Passes()
        {
            validator.Validate(Input("user@example.com", "Abcdef12", 30));
        }

        [TestMethod]
        public void EmailBlank_RejectedWithFieldAndCode()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("", "Abcdef12", 30)));
            Assert.AreEqual("email", ex.Field);
            Assert.AreEqual("must_not_be_blank", ex.Code);
        }

        [TestMethod]
        public void EmailInvalidFormat_Rejected()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("not-an-email", "Abcdef12", 30)));
            Assert.AreEqual("email", ex.Field);
            Assert.AreEqual("invalid_format", ex.Code);
        }

        [TestMethod]
        public void PasswordShorterThan8_Rejected()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("u@example.com", "Abc12", 30)));
            Assert.AreEqual("password", ex.Field);
            Assert.AreEqual("too_short", ex.Code);
        }

        [TestMethod]
        public void PasswordWithoutUppercase_Rejected()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("u@example.com", "abcdef12", 30)));
            Assert.AreEqual("password", ex.Field);
            Assert.AreEqual("missing_character_class", ex.Code);
        }

        [TestMethod]
        public void AgeBelow18_Rejected()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("u@example.com", "Abcdef12", 17)));
            Assert.AreEqual("age", ex.Field);
            Assert.AreEqual("below_minimum", ex.Code);
        }

        [TestMethod]
        public void AgeBoundary18_Passes()
        {
            validator.Validate(Input("u@example.com", "Abcdef12", 18));
        }

        [TestMethod]
        public void AgeBoundary120_Passes()
        {
            validator.Validate(Input("u@example.com", "Abcdef12", 120));
        }

        [TestMethod]
        public void Age121_Rejected()
        {
            var ex = Assert.ThrowsException<ValidationException>(() =>
                    validator.Validate(Input("u@example.com", "Abcdef12", 121)));
            Assert.AreEqual("age", ex.Field);
            Assert.AreEqual("above_maximum", ex.Code);
        }
    }
}
