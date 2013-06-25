using System;
using System.Diagnostics;

namespace Tucson.MongoClient {
    /// <summary>
    /// Represents a Mongo document's ObjectId
    /// </summary>
    [System.ComponentModel.TypeConverter(typeof(ObjectIdTypeConverter))]
    public class ObjectId {

        #region private fields
        private string _string;
        private byte[] _value;

        /// <summary>Sets the value.</summary>
        /// <param name="value">The value.</param>
        private void SetValue(byte[] value) {
            _value = value;
        }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectId"/> class.
        /// </summary>
        public ObjectId() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectId"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        public ObjectId(string value)
            : this(DecodeHex(value)) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectId"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        internal ObjectId(byte[] value) {
            SetValue(value);
        }

        #region public fields
        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal
        /// to this instance.
        /// </summary>
        /// <param name="obj">
        /// The <see cref="System.Object"/> to compare with this instance.
        /// </param>
        /// <returns>
        /// <c>true</c> if the specified <see cref="System.Object"/> is equal to 
        /// this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj) {
            var other = obj as ObjectId;
            return Equals(other);
        }

        /// <summary>Equalses the specified other.</summary>
        /// <param name="other">The other.</param>
        /// <returns>The equals.</returns>
        public bool Equals(ObjectId other) {
            return other != null && ToString() == other.ToString();
        }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing 
        /// algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode() {
            return GetValue() == null ? 0 : ToString().GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString() {
            if (_string == null && GetValue() != null) {
                _string = BitConverter.ToString(GetValue()).Replace("-", string.Empty)
                    .ToUpperInvariant();
            }
            return _string;
        }

        /// <summary>Gets the value.</summary>
        /// <returns>The value.</returns>
        public byte[] GetValue() {
            return _value;
        }
        #endregion

        /// <summary>
        /// Provides an empty ObjectId (all zeros).
        /// </summary>
        public static ObjectId Empty {
            get { return new ObjectId("000000000000000000000000"); }
        }

        /// <summary>
        /// Generates a new unique oid for use with MongoDB Objects.
        /// </summary>
        /// <returns>
        /// </returns>
        public static ObjectId NewObjectId() {            
            return new ObjectId(ObjectIdGenerator.Generate());
        }

        /// <summary>Tries to parse an ObjectId from a string.</summary>
        /// <param name="value">The string to parse.</param>
        /// <param name="id">The ObjectId.</param>
        /// <returns>The try parse result.</returns>
        public static bool TryParse(string value, out ObjectId id) {
            id = Empty;
            if (value == null || value.Length != 24) {
                return false;
            }

            try {
                id = new ObjectId(value);
                return true;
            }
            catch (FormatException) {
                return false;
            }
        }

        /// <summary>Implements the operator ==.</summary>
        /// <param name="objectA">The first ObjectId.</param>
        /// <param name="objectB">The second ObjectId.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(ObjectId objectA, ObjectId objectB) {
            if (ReferenceEquals(objectA, objectB)) {
                return true;
            }

            if (((object)objectA == null) || ((object)objectB == null)) {
                return false;
            }

            return objectA.Equals(objectB);
        }

        /// <summary>Implements the operator !=.</summary>
        /// <param name="objectA">The first ObjectId.</param>
        /// <param name="objectB">The second ObjectId.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(ObjectId objectA, ObjectId objectB) {
            return !(objectA == objectB);
        }

        /// <summary>
        /// Implements the implicit conversion from ObjectId to string.
        /// </summary>
        /// <param name="objectId">ObjectId to implicitly convert.</param>
        /// <returns>Converted string.</returns>
        public static implicit operator string(ObjectId objectId) {
            return objectId == null ? null : objectId.ToString();
        }

        /// <summary>
        /// Implements the implicit conversion from string to ObjectId.
        /// </summary>
        /// <param name="value">String to implicitly convert.</param>
        /// <returns>Converted ObjectId.</returns>
        public static implicit operator ObjectId(String value) {
            var retval = Empty;
            if (!String.IsNullOrEmpty(value)) {
                retval = new ObjectId(value);
            }
            return retval;
        }

        /// <summary>Decodes a HexString to bytes.</summary>
        /// <param name="value">
        /// The hex encoding string that should be converted to bytes.
        /// </param>
        /// <returns>A byte array of the string.
        /// </returns>
        protected static byte[] DecodeHex(string value) {
            if (value == null)
                return null;

            var chars = value.ToCharArray();
            var numberChars = chars.Length;
            var bytes = new byte[numberChars/2];

            for (var i = 0; i < numberChars; i += 2)
            {
                bytes[i/2] = Convert.ToByte(new string(chars, i, 2), 16);
            }
            return bytes;
        }
    }
}