// Fronts & Rivals: "The Office" contact on the phone (iFruitAddon2). Calling it opens the Office menu.
// Main only touches this class through PhoneHost, so a missing iFruitAddon2.dll can't stop the whole mod.
using System;
using iFruitAddon2;

namespace FrontsAndRivals.UI
{
    public sealed class PhoneContact
    {
        private readonly CustomiFruit _phone = new CustomiFruit();

        public PhoneContact(Action open)
        {
            var office = new iFruitContact("The Office")
            {
                DialTimeout = 1500, // ms before the call connects
                Active = true,
                Icon = ContactIcon.Blank
            };
            office.Answered += contact =>
            {
                _phone.Close(500); // put the phone away so it doesn't take the menu's input
                open();
            };
            _phone.Contacts.Add(office);
        }

        /// <summary>Call every tick.</summary>
        public void Update() { _phone.Update(); }
    }

    /// <summary>Loads the phone contact in its own method, so if iFruitAddon2.dll isn't installed the
    /// failure (FileNotFoundException when the method is compiled) is caught and only the contact is lost.</summary>
    public static class PhoneHost
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object TryCreate(Action open)
        {
            return new PhoneContact(open);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Update(object contact)
        {
            ((PhoneContact)contact).Update();
        }
    }
}
